﻿using UnityEngine;
using System.Collections.Generic;

namespace Telescopes
{
    [System.Serializable]
    public class TelescopingSegment : MonoBehaviour
    {
        public Material material;

        public GameObject fountainPrefab;

        [Tooltip("The direction of the first shell.")]
        public Vector3 initialDirection = Vector3.up;
        [Tooltip("How many shells will be in the structure.")]
        public int numShells = 4;
        [Tooltip("The length of the first shell.")]
        public float initialLength = 1;
        [Tooltip("The radius of the first shell.")]
        public float initialRadius = 0.5f;
        [Tooltip("The amount of curvature of the structure.")]
        public float initialCurvature = 0f;
        [Tooltip("The angle that the curvature tends toward. 0 degrees = up.")]
        public float curvatureRotation = 0f;

        [Tooltip("How thick the walls of the geometry are to be.")]
        public float wallThickness = 0.1f;

        [Tooltip("The resolution of the mesh -- how many loops each cylinder should have.")]
        public static int cutsPerCylinder = 10;
        public static int verticesPerCircle = 40;

        public List<TelescopeParameters> parameters;
        public List<TelescopeParameters> concreteParameters;

        private List<TelescopingShell> shells;

        public TelescopeParameters DefaultChildDiff
        {
            get
            {
                TelescopeParameters tp = new TelescopeParameters(0, -wallThickness, wallThickness, 0, 0);
                return tp;
            }
        }

        public TelescopingShell addChildShell(TelescopingShell parent,
            TelescopeParameters parentParams, TelescopeParameters childParams)
        {
            int i = shells.Count;
            // Make the new shell, and set the previous shell as its parent
            GameObject shellObj = new GameObject();
            shellObj.transform.parent = parent.transform;
            shellObj.name = "shell" + i;

            // Make the geometry, etc.
            TelescopingShell newShell = shellObj.AddComponent<TelescopingShell>();
            newShell.GenerateGeometry(childParams);
            newShell.setMaterial(material);

            // Set the shell's rest transformation relative to its parent.
            // When the shell's current extension ratio is 0, this is where
            // it is located relative to its parent.
            // newShell.baseRadians = newShell.radiansOfLength(wallThickness);
            newShell.baseTranslation = TelescopeUtils.childBasePosition(parentParams, childParams);
            newShell.baseRotation = TelescopeUtils.childBaseRotation(parentParams, childParams);
            shells.Add(newShell);

            CapsuleCollider cc = shellObj.AddComponent<CapsuleCollider>();
            cc.direction = 2;
            shellObj.layer = 8;
            Rigidbody rb = shellObj.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            newShell.containingSegment = this;

            return newShell;
        }

        public void MakeAllShells(List<TelescopeParameters> paramList)
        {
            // Create an object for the first shell
            GameObject rootShellObj = new GameObject();
            rootShellObj.name = "shell0";
            rootShellObj.transform.parent = this.transform;
            rootShellObj.transform.localPosition = Vector3.zero;

            // Make the shell geometry
            TelescopingShell shell = rootShellObj.AddComponent<TelescopingShell>();
            shell.GenerateGeometry(paramList[0]);
            shell.setMaterial(material);

            // Shells don't know anything about their position/rotation,
            // so we set that here.
            Quaternion rotation = Quaternion.FromToRotation(Vector3.forward, initialDirection);
            Quaternion roll = Quaternion.AngleAxis(paramList[0].twistFromParent, initialDirection);
            rootShellObj.transform.rotation = roll * rotation;
            CapsuleCollider cc = rootShellObj.AddComponent<CapsuleCollider>();
            cc.direction = 2;
            rootShellObj.layer = 8;
            Rigidbody rb = rootShellObj.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            shell.containingSegment = this;

            // shell.baseRadians = 0;
            shell.baseTranslation = Vector3.zero;
            shell.baseRotation = Quaternion.identity;

            shells.Add(shell);
            shell.isRoot = true;

            // Make all of the child shells here.
            TelescopingShell prevShell = shell;
            TelescopeParameters previousParams = paramList[0];
            TelescopeParameters currentParams = paramList[0];
            for (int i = 1; i < numShells; i++)
            {
                // Get the computed parameters for this and the previous shell.
                currentParams = paramList[i];
                previousParams = paramList[i - 1];

                // Add it.
                prevShell = addChildShell(prevShell, previousParams, currentParams);
            }

            /*
            if (fountainPrefab)
            {
                GameObject fountain = Instantiate<GameObject>(fountainPrefab);
                fountain.transform.parent = prevShell.transform;
            }*/
        }

        // Use this for initialization
        void Start()
        {
            shells = new List<TelescopingShell>();
            initialDirection.Normalize();

            Debug.Log("Num params = " + parameters.Count);

            // Compute the absolute parameter values from the list of diffs we are given.
            List<TelescopeParameters> concreteParams = new List<TelescopeParameters>();
            TelescopeParameters theParams = parameters[0];
            concreteParams.Add(new TelescopeParameters(parameters[0]));
            for (int i = 1; i < numShells; i++)
            {
                theParams = theParams + parameters[i];
                theParams.length -= 0; // wallThickness;
                theParams.radius -= wallThickness;
                concreteParams.Add(theParams);
            }

            // Make a pass in reverse that grows each parent so that it is large enough
            // to contain its child.
            for (int i = numShells - 1; i > 0; i--)
            {
                TelescopeUtils.growParentToChild(concreteParams[i - 1], concreteParams[i]);
            }

            // Construct all of the shells from this parameter list.
            MakeAllShells(concreteParams);
            concreteParameters = concreteParams;
        }


        // Update is called once per frame
        void Update()
        {
            if (Input.GetKeyDown("e"))
            {
                foreach (TelescopingShell ts in shells)
                {
                    ts.extendToRatio(1, 2f);
                }
            }
            else if (Input.GetKeyDown("q"))
            {
                foreach (TelescopingShell ts in shells)
                {
                    ts.extendToRatio(0, 2f);
                }
            }
            // Live-update the orientation for better testing
            /*
            initialDirection.Normalize();
            Quaternion rotation = Quaternion.FromToRotation(Vector3.forward, initialDirection);
            Quaternion roll = Quaternion.AngleAxis(curvatureRotation, initialDirection);
            shells[0].transform.rotation = roll * rotation;*/
        }
    }

    [System.Serializable]
    public class TelescopeParameters
    {
        public float length;
        public float radius;
        public float thickness;
        public float curvature;
        public float twistFromParent;

        public TelescopeParameters(float l, float r, float w, float c, float t)
        {
            length = l;
            radius = r;
            thickness = w;
            curvature = c;
            twistFromParent = t;
        }

        public static TelescopeParameters operator +(TelescopeParameters t1, TelescopeParameters t2)
        {
            TelescopeParameters sum = new TelescopeParameters(t1, t2);
            return sum;
        }

        public TelescopeParameters(TelescopeParameters toCopy)
        {
            length = toCopy.length;
            radius = toCopy.radius;
            thickness = toCopy.thickness;
            curvature = toCopy.curvature;
            twistFromParent = toCopy.twistFromParent;
        }

        public TelescopeParameters(TelescopeParameters baseParams, TelescopeParameters diff)
        {
            length = baseParams.length + diff.length;
            radius = baseParams.radius + diff.radius;
            thickness = baseParams.thickness;
            curvature = baseParams.curvature + diff.curvature;
            twistFromParent = diff.twistFromParent;
        }
    }
}