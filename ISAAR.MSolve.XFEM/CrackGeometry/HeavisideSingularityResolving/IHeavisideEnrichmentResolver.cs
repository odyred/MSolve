﻿using System;
using System.Collections.Generic;
using System.Text;
using ISAAR.MSolve.XFEM.Elements;
using ISAAR.MSolve.XFEM.Entities;
using ISAAR.MSolve.XFEM.Geometry.Mesh;

namespace ISAAR.MSolve.XFEM.CrackGeometry.HeavisideSingularityResolving
{
    interface IHeavisideSingularityResolver
    {
        ISet<XNode2D> FindHeavisideNodesToRemove(ISingleCrack crack, IMesh2D<XNode2D, XContinuumElement2D> mesh, 
            ISet<XNode2D> heavisideNodes);
    }
}
