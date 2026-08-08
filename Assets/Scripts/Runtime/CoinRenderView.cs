using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DecisionDisc
{
    public sealed class CoinRenderView : MonoBehaviour
    {
        private RawImage output;
        private RenderTexture renderTexture;
        private GameObject renderRoot;
        private GameObject coin;
        private Rigidbody body;
        private Mesh mesh;
        private Material frontMaterial;
        private Material backMaterial;
        private Material edgeMaterial;
        private Vector3 plannedAngularVelocity;
        private Vector3 correctionStartEuler;
        private Vector3 correctionStartVelocityDegrees;
        // Unity exposes Rigidbody rotations as wrapped Euler angles.  Keep an
        // unwrapped estimate for each throw so multi-disc stagger/correction
        // cannot erase completed revolutions at the landing phase.
        private float correctionStartFlipUnwrapped;
        private float correctionTargetFlip;
        private float correctionDuration;
        private float spinStartFlipUnwrapped;
        private float spinAccumulatedDegrees;
        private float plannedSpinTurns;
        private float plannedSpinDirection;
        private bool plannedResultReady;
        private float plannedResultTargetFlip;
        private bool deterministicSpinActive;
        private float deterministicFlip;

        public RectTransform RectTransform => (RectTransform)transform;

        public void Initialize(RawImage target, Texture yesTexture, Texture noTexture, Color edgeColor, int isolatedLayer, int resolution)
        {
            output = target;
            renderTexture = new RenderTexture(resolution, resolution, 16, RenderTextureFormat.ARGB32)
            {
                name = "DecisionDiscCoinRT",
                antiAliasing = 2,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            renderTexture.Create();
            output.texture = renderTexture;
            output.color = Color.white;

            renderRoot = new GameObject("DecisionDiscCoinRenderer");
            renderRoot.hideFlags = HideFlags.HideAndDontSave;
            renderRoot.layer = isolatedLayer;

            coin = new GameObject("Coin");
            coin.hideFlags = HideFlags.HideAndDontSave;
            coin.layer = isolatedLayer;
            coin.transform.SetParent(renderRoot.transform, false);

            // A slightly thicker rim keeps the disc readable at near-edge angles on
            // small phone screens, while still looking like a coin rather than a puck.
            mesh = CreateCoinMesh(64, 1f, .14f);
            MeshFilter filter = coin.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = coin.AddComponent<MeshRenderer>();
            MeshCollider collider = coin.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.convex = true;
            body = coin.AddComponent<Rigidbody>();
            body.useGravity = false;
            // The body is kept for the coin's 3D mesh/collider, but the visible
            // flip angle is driven explicitly below.  Interpolation must stay off:
            // Unity otherwise picks the shortest quaternion arc, which is most
            // obvious when the NO face lands.
            // Letting Rigidbody integrate the X rotation made the final face
            // depend on FixedUpdate timing and caused a backwards correction.
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.constraints = RigidbodyConstraints.FreezePosition;
            // Ten visible flips can require a higher short-burst angular velocity on
            // a light tap with high pressure.  This is still bounded and only used
            // while the disc is airborne.
            body.maxAngularVelocity = 120f;
            body.angularDrag = .18f;

            Shader coinShader = Resources.Load<Shader>("DecisionDiscCoin");
            if (coinShader == null) coinShader = Shader.Find("UI/Default");
            if (coinShader == null) throw new MissingReferenceException("Decision Disc coin shader is missing.");
            frontMaterial = new Material(coinShader) { name = "Coin YES Face", mainTexture = yesTexture };
            backMaterial = new Material(coinShader) { name = "Coin NO Face", mainTexture = noTexture };
            edgeMaterial = new Material(coinShader) { name = "Coin Edge", mainTexture = Texture2D.whiteTexture, color = edgeColor };
            // Match the static uGUI face (which has a white circular backing)
            // when the badge enters the 3D rotation.  Without this, transparent
            // pixels in a user PNG reveal the page/background as soon as the
            // coin starts moving.
            frontMaterial.SetFloat("_WhiteBacking", 1f);
            backMaterial.SetFloat("_WhiteBacking", 1f);
            edgeMaterial.SetFloat("_WhiteBacking", 0f);
            renderer.sharedMaterials = new[] { frontMaterial, backMaterial, edgeMaterial };

            GameObject cameraObject = new GameObject("Coin Camera");
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            cameraObject.layer = isolatedLayer;
            cameraObject.transform.SetParent(renderRoot.transform, false);
            cameraObject.transform.localPosition = new Vector3(0, 0, -5.2f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0, 0, 0, 0);
            camera.cullingMask = 1 << isolatedLayer;
            camera.fieldOfView = 28f;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 20f;
            camera.targetTexture = renderTexture;
            camera.allowHDR = false;
            camera.allowMSAA = true;

            GameObject lightObject = new GameObject("Coin Edge Light");
            lightObject.hideFlags = HideFlags.HideAndDontSave;
            lightObject.layer = isolatedLayer;
            lightObject.transform.SetParent(renderRoot.transform, false);
            lightObject.transform.localRotation = Quaternion.Euler(35f, -30f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.cullingMask = 1 << isolatedLayer;
            light.shadows = LightShadows.None;

            SetPose(0f, 0f, 0f);
        }

        public void SetPose(float flipDegrees, float yawDegrees, float rollDegrees)
        {
            if (coin == null) return;
            ApplyKinematicRotation(Quaternion.Euler(flipDegrees, yawDegrees, rollDegrees));
        }

        public void BeginPhysicsSpin(Vector3 angularVelocity, float turns = 0f, bool yes = true)
        {
            if (body == null) return;
            // Keep the Rigidbody kinematic.  The throw path still uses this view's
            // Rigidbody for the 3D coin object, but the flip angle itself must be
            // deterministic so one, three and five discs share the same motion.
            body.isKinematic = true;
            plannedAngularVelocity = angularVelocity;
            spinStartFlipUnwrapped = SignedDegrees(coin == null ? 0f : coin.transform.localEulerAngles.x);
            spinAccumulatedDegrees = 0f;
            plannedSpinTurns = Mathf.Max(0f, turns);
            plannedSpinDirection = Mathf.Sign(angularVelocity.x);
            plannedResultReady = plannedSpinTurns > .01f && Mathf.Abs(plannedSpinDirection) > .01f;
            plannedResultTargetFlip = plannedResultReady
                ? FaceAlignedTarget(spinStartFlipUnwrapped, plannedSpinDirection, plannedSpinTurns, yes ? 0f : 180f)
                : (yes ? 0f : 180f);
            deterministicSpinActive = plannedResultReady;
            deterministicFlip = spinStartFlipUnwrapped;
        }

        public void SetPhysicsSpinMultiplier(float multiplier)
        {
            // Kept as a compatibility shim for older callers.  Rotation is now
            // advanced through SetDeterministicSpinProgress, never by Rigidbody.
        }

        public void SetDeterministicSpinProgress(float progress)
        {
            if (coin == null || !deterministicSpinActive) return;
            float p = Mathf.Clamp01(progress);
            float eased = EaseInOutQuad(p);
            // One continuous curve carries the disc all the way to the locked
            // result angle.  Splitting the last few percent into a second
            // correction phase caused the visible landing pause.
            deterministicFlip = Mathf.Lerp(spinStartFlipUnwrapped, plannedResultTargetFlip, eased);
            spinAccumulatedDegrees = deterministicFlip - spinStartFlipUnwrapped;

            // Preserve a small amount of the old 3D wobble without allowing the
            // secondary axes to affect which face wins.  Both ends ease back to a
            // level landing pose.
            float wobble = Mathf.Sin(p * Mathf.PI);
            float yaw = wobble * plannedAngularVelocity.y * Mathf.Rad2Deg * .12f;
            float roll = wobble * plannedAngularVelocity.z * Mathf.Rad2Deg * .12f;
            ApplyKinematicRotation(Quaternion.Euler(deterministicFlip, yaw, roll));
        }

        /// <summary>
        /// Ends the already-computed spin without starting a second correction
        /// animation. The caller must first submit progress=1 so the final pose
        /// is exactly the precomputed YES/NO face.
        /// </summary>
        public void FinishDeterministicSpin()
        {
            deterministicSpinActive = false;
            plannedAngularVelocity = Vector3.zero;
            if (body != null) body.isKinematic = true;
        }

        public void BeginResultCorrection(bool yes, float duration)
        {
            correctionStartEuler = coin == null ? Vector3.zero : coin.transform.localEulerAngles;
            correctionDuration = Mathf.Max(.01f, duration);
            Vector3 worldAngularVelocity = body == null ? Vector3.zero : body.angularVelocity;
            correctionStartVelocityDegrees = worldAngularVelocity * Mathf.Rad2Deg;

            float faceAngle = yes ? 0f : 180f;
            float wrappedStart = correctionStartEuler.x;
            correctionStartFlipUnwrapped = deterministicSpinActive
                ? deterministicFlip
                : wrappedStart + 360f * Mathf.Round((spinStartFlipUnwrapped + spinAccumulatedDegrees - wrappedStart) / 360f);
            if (plannedResultReady)
            {
                // The target was locked when the spin started. Never infer a new
                // cycle from a landing-frame Rigidbody velocity or wrapped Euler.
                correctionTargetFlip = plannedResultTargetFlip;
            }
            else
            {
                float predictedStop = correctionStartFlipUnwrapped + correctionStartVelocityDegrees.x * correctionDuration * .5f;
                correctionTargetFlip = faceAngle + Mathf.Round((predictedStop - faceAngle) / 360f) * 360f;
            }

            if (body != null)
            {
                body.isKinematic = true;
            }
            deterministicSpinActive = false;
            plannedAngularVelocity = Vector3.zero;
        }

        public void CorrectToResult(float progress)
        {
            if (coin == null) return;
            float t = Mathf.Clamp01(progress);
            float level = Mathf.SmoothStep(0f, 1f, t);
            // Ease directly from the observed physics angle to the face-aligned
            // target. Hermite velocity extrapolation could overshoot and then
            // visibly jump back on the final frame.
            float flip = Mathf.Lerp(correctionStartFlipUnwrapped, correctionTargetFlip, level);
            float yaw = Mathf.LerpAngle(correctionStartEuler.y, 0f, level);
            float roll = Mathf.LerpAngle(correctionStartEuler.z, 0f, level);
            ApplyKinematicRotation(Quaternion.Euler(flip, yaw, roll));
        }

        private void ApplyKinematicRotation(Quaternion localRotation)
        {
            if (body != null)
            {
                body.isKinematic = true;
            }
            coin.transform.localRotation = localRotation;
            if (body != null)
            {
                body.Sleep();
            }
        }

        private static float EaseInOutQuad(float progress)
        {
            float p = Mathf.Clamp01(progress);
            return p < .5f
                ? 2f * p * p
                : 1f - Mathf.Pow(-2f * p + 2f, 2f) * .5f;
        }

        private static float SignedDegrees(float degrees)
        {
            degrees %= 360f;
            if (degrees > 180f) degrees -= 360f;
            return degrees;
        }

        private static float FaceAlignedTarget(float startFlip, float direction, float turns, float faceAngle)
        {
            bool isNoFace = faceAngle > 90f;
            int wholeTurns = Mathf.Clamp(Mathf.RoundToInt(turns), 1, 20);
            // Reserve the same whole-turn budget for every disc. The NO side
            // needs an additional half turn because it is the opposite face;
            // this keeps YES/NO motion paired instead of relying on a landing
            // correction to choose a different revolution count.
            float requestedDegrees = wholeTurns * 360f + (isNoFace ? 180f : 0f);
            float ideal = startFlip + direction * requestedDegrees;
            int faceCycle = Mathf.RoundToInt((ideal - faceAngle) / 360f);
            float target = faceAngle + faceCycle * 360f;
            if (direction > 0f)
            {
                while (target <= startFlip) target += 360f;
            }
            else if (direction < 0f)
            {
                while (target >= startFlip) target -= 360f;
            }
            return target;
        }

        private static Mesh CreateCoinMesh(int segments, float radius, float thickness)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var front = new List<int>();
            var back = new List<int>();
            var side = new List<int>();
            float half = thickness * .5f;

            int frontCenter = vertices.Count;
            vertices.Add(new Vector3(0, 0, -half)); normals.Add(Vector3.back); uvs.Add(new Vector2(.5f, .5f));
            int frontRing = vertices.Count;
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                float x = Mathf.Cos(angle) * radius, y = Mathf.Sin(angle) * radius;
                vertices.Add(new Vector3(x, y, -half)); normals.Add(Vector3.back); uvs.Add(new Vector2(.5f + x / (radius * 2f), .5f + y / (radius * 2f)));
            }
            for (int i = 0; i < segments; i++) { front.Add(frontCenter); front.Add(frontRing + i + 1); front.Add(frontRing + i); }

            int backCenter = vertices.Count;
            vertices.Add(new Vector3(0, 0, half)); normals.Add(Vector3.forward); uvs.Add(new Vector2(.5f, .5f));
            int backRing = vertices.Count;
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                float x = Mathf.Cos(angle) * radius, y = Mathf.Sin(angle) * radius;
                vertices.Add(new Vector3(x, y, half)); normals.Add(Vector3.forward); uvs.Add(new Vector2(.5f + x / (radius * 2f), .5f - y / (radius * 2f)));
            }
            for (int i = 0; i < segments; i++) { back.Add(backCenter); back.Add(backRing + i); back.Add(backRing + i + 1); }

            int sideStart = vertices.Count;
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector3 normal = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                vertices.Add(new Vector3(normal.x * radius, normal.y * radius, -half)); normals.Add(normal); uvs.Add(new Vector2(i / (float)segments, 0f));
                vertices.Add(new Vector3(normal.x * radius, normal.y * radius, half)); normals.Add(normal); uvs.Add(new Vector2(i / (float)segments, 1f));
            }
            for (int i = 0; i < segments; i++)
            {
                int a = sideStart + i * 2, b = a + 1, c = a + 2, d = a + 3;
                side.Add(a); side.Add(c); side.Add(b);
                side.Add(c); side.Add(d); side.Add(b);
            }

            var result = new Mesh { name = "Runtime Decision Disc" };
            result.SetVertices(vertices); result.SetNormals(normals); result.SetUVs(0, uvs);
            result.subMeshCount = 3;
            result.SetTriangles(front, 0); result.SetTriangles(back, 1); result.SetTriangles(side, 2);
            result.RecalculateBounds();
            return result;
        }

        private void OnDestroy()
        {
            if (renderTexture != null) { renderTexture.Release(); Destroy(renderTexture); }
            if (renderRoot != null) Destroy(renderRoot);
            if (frontMaterial != null) Destroy(frontMaterial);
            if (backMaterial != null) Destroy(backMaterial);
            if (edgeMaterial != null) Destroy(edgeMaterial);
            if (mesh != null) Destroy(mesh);
        }
    }
}
