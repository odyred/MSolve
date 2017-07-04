﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ISAAR.MSolve.Numerical.LinearAlgebra;
using ISAAR.MSolve.XFEM.Elements;
using ISAAR.MSolve.XFEM.Enrichments.Items;
using ISAAR.MSolve.XFEM.Entities;
using ISAAR.MSolve.XFEM.Geometry.Mesh;
using ISAAR.MSolve.XFEM.Geometry.Descriptions;
using ISAAR.MSolve.XFEM.Geometry.CoordinateSystems;
using ISAAR.MSolve.XFEM.Integration.Quadratures;
using ISAAR.MSolve.XFEM.Integration.Strategies;
using ISAAR.MSolve.XFEM.Materials;
using ISAAR.MSolve.XFEM.LinearAlgebra;

namespace ISAAR.MSolve.XFEM.Tests
{
    class XElementTests
    {
        private static XNode2D[] nodes = {
            new XNode2D(0, 20.0, 0.0),
            new XNode2D(1, 40.0, 0.0),
            new XNode2D(2, 40.0, 20.0),
            new XNode2D(3, 20.0, 20.0),

            new XNode2D(4, 20.0, -40.0),
            new XNode2D(5, 40.0, -40.0),
            new XNode2D(6, 40.0, -20.0),
            new XNode2D(7, 20.0, -20.0)
        };

        private static void IsoparametricQuad4WithCrackTest()
        {
            double E = 2e6;
            double v = 0.3;
            var material = HomogeneousElasticMaterial2D.CreateMaterialForPlainStrain(E, v);

            var integrationStrategy = new IntegrationForCrackPropagation2D(GaussLegendre2D.Order2x2,
                new RectangularSubgridIntegration2D<XContinuumElement2D>(2, GaussLegendre2D.Order2x2));
            var bodyElement = new XContinuumElement2D(IsoparametricElementType2D.Quad4, 
                new XNode2D[] { nodes[0], nodes[1], nodes[2], nodes[3]},
                integrationStrategy, material);
            var tipElement = new XContinuumElement2D(IsoparametricElementType2D.Quad4,
                new XNode2D[] { nodes[4], nodes[5], nodes[6], nodes[7] },
                integrationStrategy, material);
            var mesh = new SimpleMesh2D<XNode2D, XContinuumElement2D>(nodes, 
                new XContinuumElement2D[] { bodyElement, tipElement });

            var crack = new BasicExplicitCrack2D(mesh);
            CrackBodyEnrichment2D bodyEnrichment= new CrackBodyEnrichment2D(crack);
            CrackTipEnrichments2D tipEnrichment = new CrackTipEnrichments2D(crack);
            crack.CrackBodyEnrichment = bodyEnrichment;
            crack.CrackTipEnrichments = tipEnrichment;

            var point1 = new CartesianPoint2D(30.0, 20.0);
            var point2 = new CartesianPoint2D(30.0, -30.0);
            crack.InitializeGeometry(point1, point2);
            crack.UpdateEnrichments();

            SymmetricMatrix2D stiffnessStd = bodyElement.BuildStandardStiffnessMatrix();
            Matrix2D stiffnessStdEnr;
            SymmetricMatrix2D stiffnessEnr;
            bodyElement.BuildEnrichedStiffnessMatrices(out stiffnessStdEnr, out stiffnessEnr);

            stiffnessStd.Scale(1e-6);
            stiffnessStdEnr.Scale(1e-6);
            stiffnessEnr.Scale(1e-6);

            Console.WriteLine("Quad4 standard-standard stiffness matrix = ");
            MatrixUtilities.PrintDense(stiffnessStd);
            Console.WriteLine("Quad4 standard-enriched stiffness matrix = ");
            MatrixUtilities.PrintDense(stiffnessStdEnr);
            Console.WriteLine("Quad4 enriched-enriched stiffness matrix = ");
            MatrixUtilities.PrintDense(stiffnessEnr);
        }

        private static void IsoparametricQuad4BimaterialTest(XNode2D[] nodes)
        {
            double E = 2e6;
            double v = 0.3;

            var point1 = new CartesianPoint2D(30.0, 20.0);
            var point2 = new CartesianPoint2D(30.0, 0.0);
            Polyline2D discontinuity = new Polyline2D(point1, point2);
            MaterialInterface2D enrichmentItem = new MaterialInterface2D(discontinuity);
            var material = BiElasticMaterial2D.CreateMaterialForPlainStrain(0.5 * E, v, E, v, enrichmentItem);

            var integrationStrategy = new IntegrationForCrackPropagation2D(GaussLegendre2D.Order2x2,
                new RectangularSubgridIntegration2D<XContinuumElement2D>(2, GaussLegendre2D.Order2x2));
            var element = new XContinuumElement2D(IsoparametricElementType2D.Quad4, nodes,
                integrationStrategy, material);

            discontinuity.ElementIntersections.Add(element, new CartesianPoint2D[] { point1, point2 });
            enrichmentItem.EnrichElement(element);
            enrichmentItem.EnrichNode(nodes[0]);
            enrichmentItem.EnrichNode(nodes[1]);
            enrichmentItem.EnrichNode(nodes[2]);
            enrichmentItem.EnrichNode(nodes[3]);

            SymmetricMatrix2D stiffnessStd = element.BuildStandardStiffnessMatrix();
            Matrix2D stiffnessStdEnr;
            SymmetricMatrix2D stiffnessEnr;
            element.BuildEnrichedStiffnessMatrices(out stiffnessStdEnr, out stiffnessEnr);

            stiffnessStd.Scale(1e-6);
            stiffnessStdEnr.Scale(1e-6);
            stiffnessEnr.Scale(1e-6);

            Console.WriteLine("Quad4 standard-standard stiffness matrix = ");
            MatrixUtilities.PrintDense(stiffnessStd);
            Console.WriteLine("Quad4 standard-enriched stiffness matrix = ");
            MatrixUtilities.PrintDense(stiffnessStdEnr);
            Console.WriteLine("Quad4 enriched-enriched stiffness matrix = ");
            MatrixUtilities.PrintDense(stiffnessEnr);
        }

        static void Main(string[] args)
        {
            IsoparametricQuad4WithCrackTest();
            //IsoparametricQuad4BimaterialTest(NodeSets.nodeSet7);
        }
    }
}
