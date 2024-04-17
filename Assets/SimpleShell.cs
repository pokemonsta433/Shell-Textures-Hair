using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimpleShell : MonoBehaviour {
    public Mesh shellMesh;

    // make a copy of the mesh so we can alter it (rotate it, scale it) without changing the original
    public Mesh alterable_mesh;

    public Shader shellShader;

    public bool updateStatics = true;

    // sliders for all of our details
    [Range(1, 256)]
    public int numShells = 256;

    [Range(0.0f, 1.0f)]
    public float shellDistance = 0.644f;

    [Range(0.01f, 3.0f)]
    public float heightBias = 1.4f;

    [Range(1.0f, 1000.0f)]
    public float density = 141.0f;

    [Range(0.0f, 10.0f)]
    public float thickness = 10.0f;

    [Range(0.0f, 10.0f)]
    public float curvature = 2.59f;

    [Range(0.0f, 1.0f)]
    public float displacementStrength = 0.193f;

    public Color shellColor;

    [Range(0.0f, 5.0f)]
    public float Occlusion = 5.0f;

    [Range(0.0f, 1.0f)]
    public float occlusionBias = 0.042f;

    [Range(0.0f, 4.0f)]
    public float meshScale = 1.0f;

    // because of the way the shader works, it acts on the mesh, not the game-object
    // I need to be able to rotate models that are oriented the wrong way and still
    // have the gravity look good :)
    [Range(0, 365)]
    public int x_rotation = 0;

    [Range(0, 365)]
    public int y_rotation = 0;

    [Range(0, 365)]
    public int z_rotation = 0;

    private Material shellMaterial;
    private GameObject[] shells;

    private Vector3 displacementDirection = new Vector3(0, 0, 0);

    void OnEnable() {
        // make a changeable copy of the mesh, so we don't end up rotating/scaling our original mesh a hundred times :)
        alterable_mesh = new Mesh();
        alterable_mesh.vertices = shellMesh.vertices;
        alterable_mesh.triangles = shellMesh.triangles;
        alterable_mesh.uv = shellMesh.uv;
        alterable_mesh.normals = shellMesh.normals;
        alterable_mesh.colors = shellMesh.colors;
        alterable_mesh.tangents = shellMesh.tangents;

        if(meshScale != 1){
            // scale the mesh and also rotate it before doing any shadering
            Vector3[] verts = alterable_mesh.vertices;
            for(int v = 0; v < alterable_mesh.vertexCount; v++){
                verts[v] *= meshScale;
                alterable_mesh.vertices = verts;
                alterable_mesh.RecalculateNormals();
                // somewhere along the line I did something wrong and the gorilla now takes YEARS to recalculate normals. I have no clue what happened there
            }
        }
        if (x_rotation != 0){ // This saves some computation time if we aren't rotating, but as a side-effect you need to set x-rotation to a nonzero value if you want to rotate the mesh along any axis. Luckily, 365 is a legal rotation, so you can set it to 365 and it will still rotate. Or you can just set it to a small value like 1.
            Vector3[] verts = alterable_mesh.vertices;
            Vector3 center = new Vector3(0,0,0); // rotate around origin
            Quaternion rotationQuaternion = new Quaternion();
            rotationQuaternion.eulerAngles = new Vector3(x_rotation,y_rotation,z_rotation);
            for(int i = 0; i<verts.Length; i++) {
                verts[i] = rotationQuaternion * (verts[i] - center) + center;
            }
            alterable_mesh.vertices = verts;
            alterable_mesh.RecalculateNormals();
        }


        shellMaterial = new Material(shellShader);

        shells = new GameObject[numShells];

        for (int i = 0; i < numShells; ++i) {
            shells[i] = new GameObject("Shell " + i.ToString());
            shells[i].AddComponent<MeshFilter>();
            shells[i].AddComponent<MeshRenderer>();

            shells[i].GetComponent<MeshFilter>().mesh = alterable_mesh;
            shells[i].GetComponent<MeshRenderer>().material = shellMaterial;
            shells[i].transform.SetParent(this.transform, false);

            // values that get sent to GPU.
            shells[i].GetComponent<MeshRenderer>().material.SetInt("_NumShells", numShells);
            shells[i].GetComponent<MeshRenderer>().material.SetInt("_ShellIndex", i);
            shells[i].GetComponent<MeshRenderer>().material.SetFloat("_ShellDistance", shellDistance);
            shells[i].GetComponent<MeshRenderer>().material.SetFloat("_Density", density);
            shells[i].GetComponent<MeshRenderer>().material.SetFloat("_Thickness", thickness);
            shells[i].GetComponent<MeshRenderer>().material.SetFloat("_Occlusion", Occlusion);
            shells[i].GetComponent<MeshRenderer>().material.SetFloat("_HeightBias", heightBias);
            shells[i].GetComponent<MeshRenderer>().material.SetFloat("_Curvature", curvature);
            shells[i].GetComponent<MeshRenderer>().material.SetFloat("_DisplacementStrength", displacementStrength);
            shells[i].GetComponent<MeshRenderer>().material.SetFloat("_OcclusionBias", occlusionBias);
            shells[i].GetComponent<MeshRenderer>().material.SetVector("_ShellColor", shellColor);
        }
    }

    void Update() {
        float velocity = 1.0f;

        Vector3 direction = new Vector3(0, 0, 0);
        Vector3 oppositeDirection = new Vector3(0, 0, 0);

        // Easiest way change the gravity vector based on pressed keys
        direction.x = Convert.ToInt32(Input.GetKey(KeyCode.D)) - Convert.ToInt32(Input.GetKey(KeyCode.A)); // x axis
        direction.y = Convert.ToInt32(Input.GetKey(KeyCode.W)) - Convert.ToInt32(Input.GetKey(KeyCode.S)); // y axis
        direction.z = Convert.ToInt32(Input.GetKey(KeyCode.Q)) - Convert.ToInt32(Input.GetKey(KeyCode.E)); // z axis

        // calculate movement
        Vector3 currentPosition = this.transform.position;
        direction.Normalize();
        currentPosition += direction * velocity * Time.deltaTime;
        this.transform.position = currentPosition;

        displacementDirection -= direction * Time.deltaTime * 5.0f;
        displacementDirection.y -= 5.0f * Time.deltaTime; //gravity

        if (displacementDirection.magnitude > 1) displacementDirection.Normalize();

        // more performant to set this as a global variable once than as a property of all n shells
        Shader.SetGlobalVector("_DisplacementVector", displacementDirection);

        if (updateStatics) { // generally leave this off, but you gotta toggle it if you change the script in preview mode
            for (int i = 0; i < numShells; ++i) {
                shells[i].GetComponent<MeshRenderer>().material.SetInt("_NumShells", numShells);
                shells[i].GetComponent<MeshRenderer>().material.SetInt("_ShellIndex", i);
                shells[i].GetComponent<MeshRenderer>().material.SetFloat("_ShellDistance", shellDistance);
                shells[i].GetComponent<MeshRenderer>().material.SetFloat("_Density", density);
                shells[i].GetComponent<MeshRenderer>().material.SetFloat("_Thickness", thickness);
                shells[i].GetComponent<MeshRenderer>().material.SetFloat("_Occlusion", Occlusion);
                shells[i].GetComponent<MeshRenderer>().material.SetFloat("_HeightBias", heightBias);
                shells[i].GetComponent<MeshRenderer>().material.SetFloat("_Curvature", curvature);
                shells[i].GetComponent<MeshRenderer>().material.SetFloat("_DisplacementStrength", displacementStrength);
                shells[i].GetComponent<MeshRenderer>().material.SetFloat("_OcclusionBias", occlusionBias);
                shells[i].GetComponent<MeshRenderer>().material.SetVector("_ShellColor", shellColor);
            }
        }

    }

    void OnDisable() {
        for (int i = 0; i < shells.Length; ++i) {
            Destroy(shells[i]);
        }

        shells = null;
    }
}
