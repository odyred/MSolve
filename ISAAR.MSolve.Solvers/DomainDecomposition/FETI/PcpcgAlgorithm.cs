﻿using System;
using System.Collections.Generic;
using System.Text;
using ISAAR.MSolve.LinearAlgebra.Vectors;

//TODO: rearrange the PCPCG algorithm such that unnecessary operations are avoided.
//TODO: rearrange the PCPCG algorithm, rename variables and use CG utility classes such that PCPCG is consistent with the CG and PCG algorithms.
namespace ISAAR.MSolve.Solvers.DomainDecomposition.FETI
{
    /// <summary>
    /// Implementation of the Preconditioned Conjugate Projected Gradient Method used in the FETI method.
    /// </summary>
    internal class PcpcgAlgorithm
    {
        private readonly int maxIterations;
        private readonly double residualNormTolerance;

        internal PcpcgAlgorithm(int maxIterations, double residualNormTolerance)
        {
            this.maxIterations = maxIterations;
            this.residualNormTolerance = residualNormTolerance;
        }

        internal PcpgStatistics Solve(BoundaryFlexibilityMatrix matrix, IFetiPreconditioner preconditioner,
            ProjectorMatrix projector, Vector forces, Vector boundaryDisplacements, Vector rigidBodyMotionsWork, 
            Vector lagrangeMultipliers)
        {
            int n = matrix.Order;
            double forcesNorm = forces.Norm2();

            // λ0 = Q * G * inv(G^T * Q * G) * e
            projector.InitializeLagrangeMultipliers(rigidBodyMotionsWork, lagrangeMultipliers);

            // r0 = d - F * λ0
            var residual = Vector.CreateZero(n);
            matrix.Multiply(lagrangeMultipliers, residual);
            residual.LinearCombinationIntoThis(-1.0, boundaryDisplacements, 1.0);

            // Other allocations
            var projectedResidual = Vector.CreateZero(n);
            var preconditionedResidual = Vector.CreateZero(n);
            var preconditionedProjectedResidual = Vector.CreateZero(n);
            var direction = Vector.CreateZero(n);
            var matrixTimesDirection = Vector.CreateZero(n);
            double residualDotProductPrevious = double.NaN;
            double residualNormEstimateRatio = double.NaN;
            double beta = double.NaN;

            for (int iter = 1; iter <= maxIterations; ++iter)
            {
                // w(m-1) = P * r(m-1)
                projector.ProjectVector(residual, projectedResidual);

                // z(m-1) = preconditioner * w(m-1)
                preconditioner.SolveLinearSystem(projectedResidual, preconditionedProjectedResidual);

                // Check ||z|| / ||f|| < tolearance
                residualNormEstimateRatio = preconditionedProjectedResidual.Norm2() / forcesNorm;
                if (residualNormEstimateRatio <= residualNormTolerance) 
                {
                    //TODO: is it correct to check for convergence here? How many iterations should I return?
                    return new PcpgStatistics
                    {
                        HasConverged = true,
                        NumIterations = iter - 1,
                        ResidualNormEstimateRatio = residualNormEstimateRatio
                    };
                }

                // y(m-1) = P * z(m-1)
                projector.ProjectVector(preconditionedProjectedResidual, preconditionedResidual);

                double residualDotProductCurrent = preconditionedResidual.DotProduct(projectedResidual);

                if (iter == 1)
                {
                    // β(1) = 0
                    beta = 0;

                    // p(1) = y0
                    direction.CopyFrom(preconditionedResidual);
                }
                else
                {
                    // β(m) = (y(m-1) * w(m-1)) / (y(m-2) * w(m-2))
                    beta = residualDotProductCurrent / residualDotProductPrevious;

                    // p(m) = y(m-1) + β(m) * p(m-1), if m > 1. Else p(1) = y0
                    direction.LinearCombinationIntoThis(beta, preconditionedResidual, 1.0);
                }
                residualDotProductPrevious = residualDotProductCurrent;

                // γ(m) = (y(m-1) * w(m-1)) / (p(m) * F * p(m))
                matrix.Multiply(direction, matrixTimesDirection);
                double stepSize = direction.DotProduct(matrixTimesDirection);

                // λ(m) = λ(m-1) + γ(m) * p(m)
                lagrangeMultipliers.AxpyIntoThis(direction, stepSize);

                // r(m) = r(m-1) -γ(m) * F * p(m)
                residual.AxpyIntoThis(matrixTimesDirection, -stepSize);
            }

            return new PcpgStatistics
            {
                HasConverged = false,
                NumIterations = maxIterations,
                ResidualNormEstimateRatio = residualNormEstimateRatio
            };
        }
    }
}
