using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class vertexShaderAsPointCloud : MonoBehaviour {
    public Mesh shellMesh;

    // make a copy of the mesh so we can alter it (rotate it, scale it) without changing the original
    public Mesh alterable_mesh;

    public Shader shellShader;

    public bool updateStatics = true;

    // sliders for all of our details
    [Range(1, 256)]
    public int shellCount = 16;

    [Range(0.0f, 1.0f)]
    public float shellLength = 0.15f;

    [Range(0.01f, 3.0f)]
    public float distanceAttenuation = 1.0f;

    [Range(1.0f, 1000.0f)]
    public float density = 100.0f;

    [Range(0.0f, 1.0f)]
    public float noiseMin = 0.0f;

    [Range(0.0f, 1.0f)]
    public float noiseMax = 1.0f;

    [Range(0.0f, 10.0f)]
    public float thickness = 1.0f;

    [Range(0.0f, 10.0f)]
    public float curvature = 1.0f;

    [Range(0.0f, 1.0f)]
    public float displacementStrength = 0.1f;

    public Color shellColor;

    [Range(0.0f, 5.0f)]
    public float occlusionAttenuation = 1.0f;

    [Range(0.0f, 1.0f)]
    public float occlusionBias = 0.0f;

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
            }
        }
        if (x_rotation != 0){
            // I was really hoping to rotate things this way but no such luck
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

        shells = new GameObject[shellCount];

        for (int i = 0; i < shellCount; ++i) {
            shells[i] = new GameObject("Shell " + i.ToString());
            shells[i].AddComponent<MeshFilter>();
            shells[i].AddComponent<MeshRenderer>();

            shells[i].GetComponent<MeshFilter>().mesh = alterable_mesh;
            shells[i].GetComponent<MeshRenderer>().material = shellMaterial;
            shells[i].transform.SetParent(this.transform, false);

            // values that get sent to GPU.
            shells[i].GetComponent<MeshRenderer>().material.SetInt("_ShellCount", shellCount);
            shells[i].GetComponent<MeshRenderer>().material.SetInt("_ShellIndex", i);
            shells[i].GetComponent<MeshRenderer>().material.SetFloat("_ShellLength", shellLength);
            shells[i].GetComponent<MeshRenderer>().material.SetFloat("_Density", density);
            shells[i].GetComponent<MeshRenderer>().material.SetFloat("_Thickness", thickness);
            shells[i].GetComponent<MeshRenderer>().material.SetFloat("_Attenuation", occlusionAttenuation);
            shells[i].GetComponent<MeshRenderer>().material.SetFloat("_ShellDistanceAttenuation", distanceAttenuation);
            shells[i].GetComponent<MeshRenderer>().material.SetFloat("_Curvature", curvature);
            shells[i].GetComponent<MeshRenderer>().material.SetFloat("_DisplacementStrength", displacementStrength);
            shells[i].GetComponent<MeshRenderer>().material.SetFloat("_OcclusionBias", occlusionBias);
            shells[i].GetComponent<MeshRenderer>().material.SetFloat("_NoiseMin", noiseMin);
            shells[i].GetComponent<MeshRenderer>().material.SetFloat("_NoiseMax", noiseMax);
            shells[i].GetComponent<MeshRenderer>().material.SetVector("_ShellColor", shellColor);
        }
    }

    void Update() {
        float velocity = 1.0f;

        Vector3 direction = new Vector3(0, 0, 0);
        Vector3 oppositeDirection = new Vector3(0, 0, 0);

        // This determines the direction we are moving from wasd input. It's probably a better idea to use Unity's input system, since it handles
        // all possible input devices at once, but I did it the old fashioned way for simplicity.
        direction.x = Convert.ToInt32(Input.GetKey(KeyCode.D)) - Convert.ToInt32(Input.GetKey(KeyCode.A));
        direction.y = Convert.ToInt32(Input.GetKey(KeyCode.W)) - Convert.ToInt32(Input.GetKey(KeyCode.S));
        direction.z = Convert.ToInt32(Input.GetKey(KeyCode.Q)) - Convert.ToInt32(Input.GetKey(KeyCode.E));

        // calculate movement
        Vector3 currentPosition = this.transform.position;
        direction.Normalize();
        currentPosition += direction * velocity * Time.deltaTime;
        this.transform.position = currentPosition;

        displacementDirection -= direction * Time.deltaTime * 5.0f;
        displacementDirection.y -= 5.0f * Time.deltaTime; //gravity

        if (displacementDirection.magnitude > 1) displacementDirection.Normalize();

        // more performant to set this as a global variable once than as a property of all n shells
        Shader.SetGlobalVector("_ShellDirection", displacementDirection);

        if (updateStatics) { // generally leave this off, but you gotta toggle it if you change the script in preview mode
            for (int i = 0; i < shellCount; ++i) {
                shells[i].GetComponent<MeshRenderer>().material.SetInt("_ShellCount", shellCount);
                shells[i].GetComponent<MeshRenderer>().material.SetInt("_ShellIndex", i);
                shells[i].GetComponent<MeshRenderer>().material.SetFloat("_ShellLength", shellLength);
                shells[i].GetComponent<MeshRenderer>().material.SetFloat("_Density", density);
                shells[i].GetComponent<MeshRenderer>().material.SetFloat("_Thickness", thickness);
                shells[i].GetComponent<MeshRenderer>().material.SetFloat("_Attenuation", occlusionAttenuation);
                shells[i].GetComponent<MeshRenderer>().material.SetFloat("_ShellDistanceAttenuation", distanceAttenuation);
                shells[i].GetComponent<MeshRenderer>().material.SetFloat("_Curvature", curvature);
                shells[i].GetComponent<MeshRenderer>().material.SetFloat("_DisplacementStrength", displacementStrength);
                shells[i].GetComponent<MeshRenderer>().material.SetFloat("_OcclusionBias", occlusionBias);
                shells[i].GetComponent<MeshRenderer>().material.SetFloat("_NoiseMin", noiseMin);
                shells[i].GetComponent<MeshRenderer>().material.SetFloat("_NoiseMax", noiseMax);
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
