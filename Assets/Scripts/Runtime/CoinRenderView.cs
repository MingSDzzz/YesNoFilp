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
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
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

        public void BeginPhysicsSpin(Vector3 angularVelocity, float turns = 0f)
        {
            if (body == null) return;
            body.isKinematic = false;
            plannedAngularVelocity = angularVelocity;
            body.angularVelocity = Vector3.zero;
            spinStartFlipUnwrapped = SignedDegrees(coin == null ? 0f : coin.transform.localEulerAngles.x);
            spinAccumulatedDegrees = 0f;
            plannedSpinTurns = Mathf.Max(0f, turns);
            plannedSpinDirection = Mathf.Sign(angularVelocity.x);
        }

        public void SetPhysicsSpinMultiplier(float multiplier)
        {
            if (body == null || body.isKinematic) return;
            body.angularVelocity = plannedAngularVelocity * Mathf.Clamp01(multiplier);
            spinAccumulatedDegrees += body.angularVelocity.x * Mathf.Rad2Deg * Time.unscaledDeltaTime;
        }

        public void BeginResultCorrection(bool yes, float duration)
        {
            correctionStartEuler = coin == null ? Vector3.zero : coin.transform.localEulerAngles;
            correctionDuration = Mathf.Max(.01f, duration);
            Vector3 worldAngularVelocity = body == null ? Vector3.zero : body.angularVelocity;
            correctionStartVelocityDegrees = worldAngularVelocity * Mathf.Rad2Deg;

            float faceAngle = yes ? 0f : 180f;
            float wrappedStart = correctionStartEuler.x;
            float estimatedStart = spinStartFlipUnwrapped + spinAccumulatedDegrees;
            correctionStartFlipUnwrapped = wrappedStart + 360f * Mathf.Round((estimatedStart - wrappedStart) / 360f);
            float predictedStop = correctionStartFlipUnwrapped + correctionStartVelocityDegrees.x * correctionDuration * .5f;
            correctionTargetFlip = faceAngle + Mathf.Round((predictedStop - faceAngle) / 360f) * 360f;
            if (correctionStartVelocityDegrees.x > 1f && correctionTargetFlip < correctionStartFlipUnwrapped + 45f)
                correctionTargetFlip += 360f;
            else if (correctionStartVelocityDegrees.x < -1f && correctionTargetFlip > correctionStartFlipUnwrapped - 45f)
                correctionTargetFlip -= 360f;

            if (plannedSpinTurns > .01f && Mathf.Abs(plannedSpinDirection) > .01f)
            {
                float direction = plannedSpinDirection;
                float minimumFlip = spinStartFlipUnwrapped + direction * plannedSpinTurns * 360f;
                // Use the planned turn budget as the landing target instead of
                // extrapolating the current Rigidbody velocity.  The latter can
                // add one or two accidental extra revolutions, especially when
                // several discs are being corrected on staggered frames.
                float baseFlip = direction > 0f ? Mathf.Max(minimumFlip, correctionStartFlipUnwrapped) : Mathf.Min(minimumFlip, correctionStartFlipUnwrapped);
                if (direction > 0f)
                {
                    correctionTargetFlip = faceAngle + Mathf.Ceil((baseFlip - faceAngle) / 360f) * 360f;
                    while (correctionTargetFlip < baseFlip) correctionTargetFlip += 360f;
                    while (correctionTargetFlip < correctionStartFlipUnwrapped) correctionTargetFlip += 360f;
                }
                else
                {
                    correctionTargetFlip = faceAngle + Mathf.Floor((baseFlip - faceAngle) / 360f) * 360f;
                    while (correctionTargetFlip > baseFlip) correctionTargetFlip -= 360f;
                    while (correctionTargetFlip > correctionStartFlipUnwrapped) correctionTargetFlip -= 360f;
                }
            }

            if (body != null)
            {
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
            }
            plannedAngularVelocity = Vector3.zero;
        }

        public void CorrectToResult(float progress)
        {
            if (coin == null) return;
            float t = Mathf.Clamp01(progress);
            float t2 = t * t;
            float t3 = t2 * t;
            float h00 = 2f * t3 - 3f * t2 + 1f;
            float h10 = t3 - 2f * t2 + t;
            float h01 = -2f * t3 + 3f * t2;
            float flip = h00 * correctionStartFlipUnwrapped
                + h10 * correctionStartVelocityDegrees.x * correctionDuration
                + h01 * correctionTargetFlip;
            float level = Mathf.SmoothStep(0f, 1f, t);
            float yaw = Mathf.LerpAngle(correctionStartEuler.y, 0f, level);
            float roll = Mathf.LerpAngle(correctionStartEuler.z, 0f, level);
            ApplyKinematicRotation(Quaternion.Euler(flip, yaw, roll));
        }

        private void ApplyKinematicRotation(Quaternion localRotation)
        {
            if (body != null)
            {
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
            }
            coin.transform.localRotation = localRotation;
            if (body != null)
            {
                body.rotation = coin.transform.rotation;
                body.Sleep();
            }
        }

        private static float SignedDegrees(float degrees)
        {
            degrees %= 360f;
            if (degrees > 180f) degrees -= 360f;
            return degrees;
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
