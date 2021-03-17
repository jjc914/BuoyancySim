using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Buoyancy;

// https://gamasutra.com/view/news/237528/Water_interaction_model_for_boats_in_video_games.php#1
public class BuoyantObject : MonoBehaviour
{
    private Rigidbody rb;
    private Collider col;
    private BuoyantMesh bMesh;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        bMesh = new BuoyantMesh(this.transform);
    }

    private void FixedUpdate()
    {
        bMesh.UpdateMesh();
        //bMesh.DisplaySubmergedTriangles();

        //float submergedArea = 0f;
        //for (int i = 0; i < bMesh.CutTrianglesSubmerged.Count; i++)
        //{
        //    for (int k = 0; k < bMesh.CutTrianglesSubmerged[i].triangles.Length; k++)
        //    {
        //        submergedArea += bMesh.CutTrianglesSubmerged[i].triangles[k].area;
        //    }
        //}

        //float aboveWaterArea = 0f;
        //for (int i = 0; i < bMesh.CutTrianglesAboveWater.Count; i++)
        //{
        //    for (int k = 0; k < bMesh.CutTrianglesAboveWater[i].triangles.Length; k++)
        //    {
        //        aboveWaterArea += bMesh.CutTrianglesAboveWater[i].triangles[k].area;
        //    }
        //}

        //float r = submergedArea / (aboveWaterArea + submergedArea);

        for (int i = 0; i < bMesh.CutTrianglesSubmerged.Count; i++)
        {
            for (int k = 0; k < bMesh.CutTrianglesSubmerged[i].triangles.Length; k++)
            {
                float rho = 1027f;
                //float dampningForceStrength = 0f;

                Vector3 force = bMesh.FindBuoyancyForce(rho, bMesh.CutTrianglesSubmerged[i].triangles[k]);
                TriangleData triangleData = bMesh.CutTrianglesSubmerged[i].triangles[k];
                rb.AddForceAtPosition(force/* - (dampningForceStrength * r * force)*/, transform.TransformPoint(triangleData.center));

                //Debug.DrawRay(transform.TransformPoint(triangleData.center), transform.TransformDirection(triangleData.normal) * force.magnitude, Color.white);

                //Buoyancy
                Debug.DrawRay(transform.TransformPoint(triangleData.center), force, Color.blue);
            }
        }
    }
}
