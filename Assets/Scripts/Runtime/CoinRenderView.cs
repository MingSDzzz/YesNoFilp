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
        private Mesh mesh;
        private Material frontMaterial;
        private Material backMaterial;
        private Material edgeMaterial;

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

            Shader coinShader = Resources.Load<Shader>("DecisionDiscCoin");
            if (coinShader == null) coinShader = Shader.Find("UI/Default");
            if (coinShader == null) throw new MissingReferenceException("Decision Disc coin shader is missing.");
            frontMaterial = new Material(coinShader) { name = "Coin YES Face", mainTexture = yesTexture };
            backMaterial = new Material(coinShader) { name = "Coin NO Face", mainTexture = noTexture };
            edgeMaterial = new Material(coinShader) { name = "Coin Edge", mainTexture = Texture2D.whiteTexture, color = edgeColor };
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
            if (coin != null) coin.transform.localRotation = Quaternion.Euler(flipDegrees, yawDegrees, rollDegrees);
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
