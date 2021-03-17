using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Buoyancy
{
    public struct TriangleData
    {
        public Vector3[] vertices; // local positions
        public int[] triangles;
        public Vector3 normal;

        public Vector3 center; // local positions
        public float distance;
        public float area;

        // todo: add in normals, etc

        public TriangleData(Vector3[] vertices, int[] triangles, float[] distances, Transform transform)
        {
            if (vertices.Length != 3 || triangles.Length != 3)
            {
                throw new System.Exception();
            }

            this.vertices = vertices;
            this.triangles = triangles;

            this.normal = Vector3.Cross(vertices[1] - vertices[0], vertices[2] - vertices[1]).normalized;

            this.distance = (distances[0] + distances[1] + distances[2]) / 3f;
            this.center = (vertices[0] + vertices[1] + vertices[2]) / 3f;

            // Heron's Formula
            float a = Vector3.Distance(transform.TransformPoint(vertices[0]), transform.TransformPoint(vertices[1]));
            float b = Vector3.Distance(transform.TransformPoint(vertices[1]), transform.TransformPoint(vertices[2]));
            float c = Vector3.Distance(transform.TransformPoint(vertices[2]), transform.TransformPoint(vertices[0]));
            float p = (a + b + c) / 2f;

            // square root is slow, find another way
            this.area = Mathf.Sqrt(p * (p - a) * (p - b) * (p - c));
        }
    }

    public struct MeshData
    {
        public TriangleData[] triangles;

        public MeshData(TriangleData[] triangles)
        {
            this.triangles = triangles;
        }
    }

    public class BuoyantMesh
    {
        public GameObject displayInstance;

        private Transform transform;
        private int[] triangles;
        private Mesh mesh;

        public List<MeshData> CutTrianglesSubmerged { get; private set; }
        public List<MeshData> CutTrianglesAboveWater { get; private set; }

        private LayerMask waterMask = 1 << 4;

        public BuoyantMesh(Transform transform)
        {
            this.transform = transform;
            this.mesh = transform.GetComponent<MeshFilter>().mesh;
            this.triangles = mesh.triangles;
        }

        public void UpdateMesh()
        {
            CutTrianglesSubmerged = new List<MeshData>();
            CutTrianglesAboveWater = new List<MeshData>();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                //Debug.Log(mesh.vertices[triangles[i]]);
                (MeshData, MeshData) returnedData = GetSubmergedCutTriangle(i / 3, new Vector3[] { mesh.vertices[triangles[i]], mesh.vertices[triangles[i + 1]], mesh.vertices[triangles[i + 2]] });
                CutTrianglesSubmerged.Add(returnedData.Item1);
                CutTrianglesAboveWater.Add(returnedData.Item2);
            }
        }

        // TODO: somehow make it so that the triangles are always facing same direction
        private (MeshData, MeshData) GetSubmergedCutTriangle(int triangleNumber, Vector3[] vertices)
        {
            float[] distances = new float[3];
            List<(uint, int)> verticesFiltered = new List<(uint, int)>();
            int underwaterCount = 0;
            int aboveWaterCount = 0;

            for (int i = 0; i < vertices.Length; i++)
            {
                // FIXME: here, save draw position (?)
                // save the position of the vertex in the vertices list, then later on sort the other list by this vertex list (?)
                distances[i] = GetVertexDistanceFromWater(transform.TransformPoint(vertices[i]));
                if (distances[i] < 0)
                {
                    verticesFiltered.Add(((uint)i, -1));
                    underwaterCount++;
                }
                else
                {
                    verticesFiltered.Add(((uint)i, 1));
                    aboveWaterCount++;
                }
            }

            if (underwaterCount == 3)
            {
                //// do nothing, the entire triangle is submerged
                MeshData meshData = new MeshData(new TriangleData[] { new TriangleData(vertices, new int[] { 0, 1, 2 }, new float[] { GetVertexDistanceFromWater(transform.TransformPoint(vertices[0])), GetVertexDistanceFromWater(transform.TransformPoint(vertices[1])), GetVertexDistanceFromWater(transform.TransformPoint(vertices[2])) }, transform) });
                return (meshData, new MeshData(new TriangleData[] { }));
            }
            else if (underwaterCount == 2)
            {
                //// indices for the positive and negative vertices
                // above water 
                uint h_i = verticesFiltered.Find(a => a.Item2 > 0).Item1;
                // underwater
                uint m_i = verticesFiltered[(verticesFiltered.FindIndex(a => a.Item2 > 0) + 2) > 2 ? (verticesFiltered.FindIndex(a => a.Item2 > 0) - 1) : (verticesFiltered.FindIndex(a => a.Item2 > 0) + 2)].Item1;
                uint l_i = verticesFiltered[(verticesFiltered.FindIndex(a => a.Item2 > 0) + 1) > 2 ? (verticesFiltered.FindIndex(a => a.Item2 > 0) - 2) : (verticesFiltered.FindIndex(a => a.Item2 > 0) + 1)].Item1;

                //// local space vertex point
                // above water 
                Vector3 h = vertices[h_i];
                // underwater
                Vector3 m = vertices[m_i];
                Vector3 l = vertices[l_i];

                //// y distances between points and water
                // above water
                float hH = distances[h_i];
                // underwater
                float hM = distances[m_i];
                float hL = distances[l_i];

                //// ratios between cut point y position and side length
                float tM = -hM / (hH - hM);
                float tL = -hL / (hH - hL);

                //// find the deltas for point M and L
                Vector3 dM = tM * (h - m);
                Vector3 dL = tL * (h - l);

                //// add deltas to original position
                Vector3 cM = m + dM;
                Vector3 cL = l + dL;

                //// create new list of vertices from this triangle
                Vector3[] aboveWaterVertices = new Vector3[] { h, m, l };
                int[] aboveWaterTriangles = { 0, 1, 2 };
                TriangleData[] aboveWaterTriangleData = new TriangleData[] { new TriangleData(aboveWaterVertices, aboveWaterTriangles, new float[] { hH, hM, hL }, transform) };

                Vector3[] underwaterVertices = { l, m, cM, cL };
                int[] underwaterTriangles = { 0, 1, 2,
                                              0, 2, 3 };
                TriangleData[] underwaterTriangleData = new TriangleData[] { new TriangleData(new Vector3[] { l, m, cM }, new int[] { 0, 1, 2 }, new float[] { hL, hM, GetVertexDistanceFromWater(transform.TransformPoint(cM)) }, transform),
                                                                             new TriangleData(new Vector3[] { l, cM, cL }, new int[] {0, 1, 2 }, new float[] { hL, GetVertexDistanceFromWater(transform.TransformPoint(cM)), GetVertexDistanceFromWater(transform.TransformPoint(cL)) }, transform) };

                return (new MeshData(underwaterTriangleData), new MeshData(aboveWaterTriangleData));
            }
            else if (underwaterCount == 1)
            {
                //foreach ((uint a, int b) in verticesFiltered)
                //{
                //    Debug.Log(a);
                //    Debug.Log(b);
                //}

                //// indices for the positive and negative vertices
                // above water 
                uint m_i = verticesFiltered[(verticesFiltered.FindIndex(a => a.Item2 < 0) + 1) > 2 ? (verticesFiltered.FindIndex(a => a.Item2 < 0) - 2) : (verticesFiltered.FindIndex(a => a.Item2 < 0) + 1)].Item1;
                uint h_i = verticesFiltered[(verticesFiltered.FindIndex(a => a.Item2 < 0) + 2) > 2 ? (verticesFiltered.FindIndex(a => a.Item2 < 0) - 1) : (verticesFiltered.FindIndex(a => a.Item2 < 0) + 2)].Item1;
                // underwater
                uint l_i = verticesFiltered.Find(a => a.Item2 < 0).Item1;

                //// local space vertex point
                // above water 
                Vector3 m = vertices[m_i];
                Vector3 h = vertices[h_i];
                // underwater
                Vector3 l = vertices[l_i];

                //// y distances between points and water
                // above water
                float hM = distances[m_i];
                float hH = distances[h_i];
                // underwater
                float hL = distances[l_i];

                //// ratios between cut point y position and side length
                float tM = -hL / (hM - hL);
                float tL = -hL / (hH - hL);

                //// find the deltas for point M and L
                Vector3 dM = tM * (l - m);
                Vector3 dH = tL * (l - h);

                //// add deltas to original position
                Vector3 cM = l - dM;
                Vector3 cH = l - dH;

                //// create new list of vertices from this triangle
                Vector3[] aboveWaterVertices = { m, h, cH, cM };
                int[] aboveWaterTriangles = { 0, 1, 2,
                                              0, 2, 3 };
                TriangleData[] aboveWaterTriangleData = new TriangleData[] { new TriangleData(new Vector3[] { m, h, cH }, new int[] { 0, 1, 2 }, new float[] { hM, hH, GetVertexDistanceFromWater(transform.TransformPoint(cH)) }, transform),
                                                                             new TriangleData(new Vector3[] { m, cH, cM }, new int[] {0, 1, 2 }, new float[] { hM, GetVertexDistanceFromWater(transform.TransformPoint(cH)), GetVertexDistanceFromWater(transform.TransformPoint(cM)) }, transform) };

                Vector3[] underwaterVertices = { cH, l, cM };
                int[] underwaterTriangles = { 0, 1, 2 };
                TriangleData[] underwaterTriangleData = new TriangleData[] { new TriangleData(underwaterVertices, underwaterTriangles, new float[] { GetVertexDistanceFromWater(transform.TransformPoint(cH)), hL, GetVertexDistanceFromWater(transform.TransformPoint(cM)) }, transform) };

                return (new MeshData(underwaterTriangleData), new MeshData(aboveWaterTriangleData));
            }
            else if (underwaterCount == 0)
            {
                // do nothing, the entire triangle is not submerged
                MeshData meshData = new MeshData(new TriangleData[] { new TriangleData(vertices, new int[] { 0, 1, 2 }, new float[] { GetVertexDistanceFromWater(transform.TransformPoint(vertices[0])), GetVertexDistanceFromWater(transform.TransformPoint(vertices[1])), GetVertexDistanceFromWater(transform.TransformPoint(vertices[2])) }, transform) });
                return (new MeshData(new TriangleData[] { }), meshData);
            }
            else
            {
                throw new System.Exception();
            }
        }

        public void DisplaySubmergedTriangles()
        {
            if (CutTrianglesSubmerged == null)
                throw new System.Exception();

            if (displayInstance == null)
            {
                displayInstance = new GameObject();

                displayInstance.AddComponent<MeshFilter>();
                displayInstance.AddComponent<MeshRenderer>();
            }
            Mesh displayMesh = displayInstance.GetComponent<MeshFilter>().mesh;
            displayMesh.Clear();

            displayInstance.transform.position = transform.position;
            displayInstance.transform.rotation = transform.rotation;

            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            for (int i = 0; i < CutTrianglesSubmerged.Count; i++)
            {
                for (int k = 0; k < CutTrianglesSubmerged[i].triangles.Length; k++)
                {
                    foreach (int trianglePoint in CutTrianglesSubmerged[i].triangles[k].triangles)
                    {
                        triangles.Add(trianglePoint + vertices.Count);
                    }
                    vertices = vertices.Concat(CutTrianglesSubmerged[i].triangles[k].vertices).ToList();
                }
            }

            displayMesh.Clear();

            displayMesh.vertices = vertices.ToArray();
            displayMesh.triangles = triangles.ToArray();

            displayMesh.RecalculateBounds();
        }

        private float GetVertexDistanceFromWater(Vector3 vertex)
        {
            RaycastHit hit;
            if (Physics.Raycast(vertex, Vector3.up, out hit, Mathf.Infinity, waterMask))
            {
                Debug.DrawLine(vertex, hit.point, Color.red);
                return -hit.distance;
            }

            if (Physics.Raycast(vertex, -Vector3.up, out hit, Mathf.Infinity, waterMask))
            {
                Debug.DrawLine(vertex, hit.point, Color.green);
                return hit.distance;
            }

            return 0f;
        }

        public Vector3 FindBuoyancyForce(float rho, TriangleData triangleData)
        {
            /*
        private Vector3 BuoyancyForce(float rho, TriangleData triangleData)
        {
            //Buoyancy is a hydrostatic force - it's there even if the water isn't flowing or if the boat stays still

            // F_buoyancy = rho * g * V
            // rho - density of the mediaum you are in
            // g - gravity
            // V - volume of fluid directly above the curved surface 

            // V = z * S * n 
            // z - distance to surface
            // S - surface area
            // n - normal to the surface
            Vector3 buoyancyForce = rho * Physics.gravity.y * triangleData.distanceToSurface * triangleData.area * triangleData.normal;

            //The vertical component of the hydrostatic forces don't cancel out but the horizontal do
            buoyancyForce.x = 0f;
            buoyancyForce.z = 0f;

            return buoyancyForce;
             * */
            
            Vector3 force = rho * Physics.gravity.y * Mathf.Abs(triangleData.distance) * triangleData.area * transform.TransformDirection(triangleData.normal);
            force.x = 0f;
            force.z = 0f;
            return force;
        }
    }
}