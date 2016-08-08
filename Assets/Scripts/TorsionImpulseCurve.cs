﻿using UnityEngine;
using System.Collections.Generic;

namespace Telescopes
{
    [RequireComponent(typeof(LineRenderer))]
    public class TorsionImpulseCurve : MonoBehaviour
    {
        List<CurveSegment> segments;
        LineRenderer lRenderer;

        List<Vector3> displayPoints;

        public void InitFromData(List<float> impulses, List<float> arcSteps,
            float curvature, float torsion,
            OrthonormalFrame initialFrame, Vector3 initialPos)
        {
            displayPoints = new List<Vector3>();
            lRenderer = GetComponent<LineRenderer>();
            segments = new List<CurveSegment>();
            // Create the initial segment
            CurveSegment prevHelix = new CurveSegment(initialPos, curvature, -torsion, 0, arcSteps[0], initialFrame);
            segments.Add(prevHelix);
            AddPointsOfSegment(prevHelix);

            float len = arcSteps[0];

            // For each impulse, make a new segment rotated by that angle.
            for (int i = 1; i < impulses.Count; i++)
            {
                float impulse = impulses[i];
                float arcStep = arcSteps[i];
                // New start point is the end of the previous curve segment.
                Vector3 newBase = TransformedHelixPoint(prevHelix, prevHelix.arcLength);
                // New frame is the frame rotated to the end of the segment.
                OrthonormalFrame newFrame = TransformedHelixFrame(prevHelix, prevHelix.arcLength);

                // Apply the torsion impulse as well.
                Quaternion impulseRot = Quaternion.AngleAxis(Mathf.Rad2Deg * impulse, newFrame.T);
                newFrame = newFrame.RotatedBy(impulseRot);

                prevHelix = new CurveSegment(newBase, curvature, -torsion, impulse, arcStep, newFrame);

                len += arcStep;

                AddPointsOfSegment(prevHelix);
                segments.Add(prevHelix);
            }

            // Add the last point of the last curve
            displayPoints.Add(TransformedHelixPoint(prevHelix, arcSteps[arcSteps.Count - 1]));

            // Set up line renderer
            lRenderer.SetVertexCount(displayPoints.Count);
            lRenderer.SetPositions(displayPoints.ToArray());
            lRenderer.SetWidth(0.1f, 0.1f);
        }

        public void SetMaterial(Material mat)
        {
            lRenderer.material = mat;
        }

        public void SetColor(Color c)
        {
            lRenderer.SetColors(c, c);
        }

        void AddPointsOfSegment(CurveSegment seg)
        {
            // Sampple the helix roughly every 0.1 distance
            int numSegments = Mathf.CeilToInt(seg.arcLength / 0.1f);
            float segLength = seg.arcLength / numSegments;
            // Sample the points.
            float cumulativeLength = 0;
            for (int i = 0; i < numSegments; i++)
            {
                displayPoints.Add(TransformedHelixPoint(seg, cumulativeLength));
                cumulativeLength += segLength;
            }
        }

        OrthonormalFrame TransformedHelixFrame(CurveSegment cs, float arcLen)
        {
            // Apply the local helix rotation.
            Quaternion oldRot = Quaternion.LookRotation(cs.frame.T, cs.frame.N);
            Quaternion newRot = oldRot *
                TelescopeUtils.RotateAlongHelix(cs.curvature, cs.torsion, arcLen)
                * Quaternion.Inverse(oldRot);
            OrthonormalFrame newFrame = cs.frame.RotatedBy(newRot);
            return newFrame;
        }

        Vector3 TransformedHelixPoint(CurveSegment cs, float arcLen)
        {
            // Get the helix point in local coordinates
            Vector3 helixPoint = TelescopeUtils.TranslateAlongHelix(cs.curvature, cs.torsion, arcLen);
            // Transform to world
            Quaternion rotateToWorld = Quaternion.LookRotation(cs.frame.T, cs.frame.N);
            Vector3 world = rotateToWorld * helixPoint + cs.startPosition;
            return world;
        }

        void MakeTelescope(float startRadius)
        {
            List<TelescopeParameters> tParams = new List<TelescopeParameters>();

            CurveSegment initialSeg = segments[0];

            TelescopeParameters initial = new TelescopeParameters(initialSeg.arcLength, startRadius,
                Constants.DEFAULT_WALL_THICKNESS, initialSeg.curvature, initialSeg.torsion, initialSeg.impulse);
            tParams.Add(initial);

            float prevLength = initialSeg.arcLength;
            float wallThickness = Constants.DEFAULT_WALL_THICKNESS;

            float currRadius = startRadius;

            for (int i = 1; i < segments.Count; i++)
            {
                float nextLength = segments[i].arcLength;
                float lengthDiff = nextLength - prevLength;
                prevLength = nextLength;
                currRadius -= wallThickness;

                TelescopeParameters p = new TelescopeParameters(segments[i].arcLength, currRadius,
                    wallThickness, segments[i].curvature, segments[i].torsion,
                    -segments[i].impulse * Mathf.Rad2Deg);
                tParams.Add(p);
            }

            GameObject obj = new GameObject();
            obj.name = "curveApproxTelescope";
            obj.transform.position = segments[0].startPosition;

            TelescopingSegment segment = obj.AddComponent<TelescopingSegment>();
            segment.paramMode = SegmentParametersMode.Concrete;
            segment.material = DesignerController.instance.defaultTelescopeMaterial;
            segment.initialDirection = segments[0].frame.T;
            segment.initialUp = segments[0].frame.N;

            segment.MakeShellsFromConcrete(tParams);
        }

        DiscreteCurve ToDiscreteCurve()
        {
            DiscreteCurve[] old = FindObjectsOfType<DiscreteCurve>();
            foreach (DiscreteCurve curve in old) Destroy(curve.gameObject);

            GameObject obj = new GameObject();
            obj.name = "ImpulseDCurve";
            DiscreteCurve dc = obj.AddComponent<DiscreteCurve>();

            dc.InitFromPoints(displayPoints, 0.1f);
            
            LineRenderer lr = dc.GetComponent<LineRenderer>();
            lr.SetWidth(0.1f, 0.1f);
            lr.material = lRenderer.material;
            lr.SetColors(Color.white, Color.white);

            Destroy(gameObject);
            return dc;
        }

        void Update()
        {
            if (Input.GetKeyDown("i"))
            {
                MakeTelescope(segments.Count * Constants.DEFAULT_WALL_THICKNESS + 0.1f);
            }

            if (Input.GetKeyDown("u"))
            {
                ToDiscreteCurve();
            }
        }
    }

    struct CurveSegment
    {
        public Vector3 startPosition;
        // Constant curvature throughout this segment.
        public float curvature;
        // Torsion impulse relative to the previous segment.
        public float impulse;
        // Arc measured from the beginning of this segment.
        public float arcLength;
        // Orthonormal frame at the start of this segment.
        public OrthonormalFrame frame;
        // Constant torsion throughout this segment.
        public float torsion;

        public CurveSegment(Vector3 basePos, float curv, float tors, float imp, float len, OrthonormalFrame f)
        {
            startPosition = basePos;
            curvature = curv;
            torsion = tors;
            impulse = imp;
            arcLength = len;
            frame = f;
        }

        public void PrintSegment()
        {
            Debug.Log("Curve segment (start = " + startPosition + ", c = " + curvature +
                ", i = " + impulse + ", a = " + arcLength + ", t = " + torsion + ")");
        }
    } 
}