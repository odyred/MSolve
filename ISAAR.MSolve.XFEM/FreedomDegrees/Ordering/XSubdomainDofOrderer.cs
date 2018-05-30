﻿using System;
using System.Collections.Generic;
using System.Text;
using ISAAR.MSolve.LinearAlgebra.Reordering;
using ISAAR.MSolve.LinearAlgebra.Vectors;
using ISAAR.MSolve.XFEM.CrackGeometry;
using ISAAR.MSolve.XFEM.Elements;
using ISAAR.MSolve.XFEM.Enrichments.Items;
using ISAAR.MSolve.XFEM.Entities;
using ISAAR.MSolve.XFEM.Exceptions;
using ISAAR.MSolve.XFEM.Geometry.CoordinateSystems;
using ISAAR.MSolve.XFEM.Geometry.Mesh;
using ISAAR.MSolve.XFEM.Geometry.Triangulation;
using ISAAR.MSolve.XFEM.Interpolation;
using ISAAR.MSolve.XFEM.Utilities;

//TODO: these singularHeavisideNodes affect all operations. Perhaps it would be more efficient to keep a local view of the 
//enriched nodes and alter that one. Another approach is to assign -1 for these dofs and check that 
namespace ISAAR.MSolve.XFEM.FreedomDegrees.Ordering
{
    //TODO: perhaps the subdomain should do all these itself
    class XSubdomainDofOrderer
    {
        //TODO: perhaps the rows, columns of the 2 tables can be shared, for less memory and faster simultaneous iteration
        private readonly DofTable<EnrichedDof> globalEnrichedDofs;
        private readonly DofTable<EnrichedDof> subdomainEnrichedDofs;

        private XSubdomainDofOrderer(int numEnrichedDofs, int firstGlobalDofIndex, DofTable<EnrichedDof> subdomainEnrichedDofs,
            DofTable<EnrichedDof> globalEnrichedDofs, 
            Dictionary<XNode2D, HashSet<IEnrichmentItem2D>> singularHeavisideEnrichments, 
            Dictionary<XNode2D, ISet<EnrichedDof>> boundaryDofs)
        {
            this.FirstGlobalDofIndex = firstGlobalDofIndex;
            this.NumEnrichedDofs = numEnrichedDofs;
            this.subdomainEnrichedDofs = subdomainEnrichedDofs;
            this.globalEnrichedDofs = globalEnrichedDofs;
            this.SingularHeavisideEnrichments = singularHeavisideEnrichments;
            this.BoundaryDofs = boundaryDofs;
        }

        public IReadOnlyDictionary<XNode2D, ISet<EnrichedDof>> BoundaryDofs { get; }
        public int FirstGlobalDofIndex { get; }
        public int NumEnrichedDofs { get; }
        /// <summary>
        /// These are boundary nodes which are enriched with Heaviside, but all Gauss points in the intersection of their 
        /// nodal support with this subdomain lie on the same side of the crack. Thus H(x)-H(xNode) = 0 and the stiffness 
        /// matrices would have 0 rows. Thus they must be avoided during dof ordering and all other operations of this object.
        /// </summary>
        public Dictionary<XNode2D, HashSet<IEnrichmentItem2D>> SingularHeavisideEnrichments { get; } //TODO: this should probably be a HashSet


        public static XSubdomainDofOrderer CreateNodeMajor(ICrackDescription crack, XSubdomain2D subdomain, 
            int globalIndicesStart) //TODO: also add AMD reordering
        {
            // Handle nodes with singular Heaviside enrichment
            Dictionary<XNode2D, HashSet<IEnrichmentItem2D>> singularityHeavisideEnrichments = 
                FindBoundaryNodesWithSingularHeaviside(crack, subdomain);
            if (singularityHeavisideEnrichments.Count > 0)
            {
                Console.Write($"WARNING: Subdomain {subdomain.ID} has boundary nodes that are enriched with Heaviside,");
                Console.Write(" but that would lead to singular matrices, since the nodal support in this subdomain only");
                Console.Write(" contains Gauss points with the same sign. It would be better to use another domain");
                Console.Write(" decomposition. The nodes in question are: ");
                foreach (var node in singularityHeavisideEnrichments.Keys) Console.Write(node + " ");
                Console.WriteLine();
            }

            var subdomainEnrichedDofs = new DofTable<EnrichedDof>();
            var globalEnrichedDofs = new DofTable<EnrichedDof>();
            var boundaryDofs = new Dictionary<XNode2D, ISet<EnrichedDof>>();
            int dofCounter = 0;

            foreach (XNode2D node in subdomain.AllNodes)
            {
                bool isboundaryNode = subdomain.BoundaryNodes.Contains(node);
                if (isboundaryNode && node.IsEnriched)
                {
                    boundaryDofs.Add(node, new HashSet<EnrichedDof>());
                }
                bool isSingularityNode = singularityHeavisideEnrichments.TryGetValue(node, 
                    out HashSet<IEnrichmentItem2D> singularEnrichments);

                foreach (IEnrichmentItem2D enrichment in node.EnrichmentItems.Keys)
                {
                    if (isSingularityNode && singularEnrichments.Contains(enrichment)) continue;
                    foreach (EnrichedDof dof in enrichment.Dofs)
                    {
                        if (isboundaryNode) boundaryDofs[node].Add(dof);
                        subdomainEnrichedDofs[node, dof] = dofCounter;
                        globalEnrichedDofs[node, dof] = globalIndicesStart + dofCounter; // Then I could just subtract the offest to go local -> global
                        ++dofCounter;
                    }
                }
            }
            return new XSubdomainDofOrderer(dofCounter, globalIndicesStart, subdomainEnrichedDofs, globalEnrichedDofs,
                singularityHeavisideEnrichments, boundaryDofs);
        }

        /// <summary>
        /// </summary>
        /// <param name="element"></param>
        /// <param name="globalFreeVector">Both the free standard and enriched dofs.</param>
        /// <returns></returns>
        public Vector ExtractEnrichedDisplacementsOfElementFromGlobal(XCluster2D cluster, XContinuumElement2D element,
            Vector globalFreeVector)
        {
            // While Heaviside dofs on boundary nodes that would cause singular stiffness matrices are generally avoided,
            // the returned vector of this method must include them. Since H(x)-H(xNode)=0, it wouldn't be wrong to exclude
            // them in theory. However it would cause indexing problems if they are missing, since other XFEM classes (such as
            // XContinuumElement) have no concept of a node being enriched only for some elements.
            DofTable<EnrichedDof> elementDofs = element.GetEnrichedDofs();
            double[] elementVector = new double[elementDofs.EntryCount];
            foreach (XNode2D node in element.Nodes)
            {
                bool isSingularityNode = SingularHeavisideEnrichments.TryGetValue(node, 
                    out HashSet<IEnrichmentItem2D> singularEnrichments);
                foreach (IEnrichmentItem2D enrichment in node.EnrichmentItems.Keys)
                {
                    if (isSingularityNode && singularEnrichments.Contains(enrichment))
                    {
                        // Find global dof index from other subdomain, sine it is not stored here
                        XSubdomain2D otherSubdomain = FindSubdomainWhereNodeEnrichmentIsNotSingular(cluster, node, enrichment);
                        foreach (var dofType in enrichment.Dofs)
                        {
                            int elementDofIdx = elementDofs[node, dofType];
                            int globalDofIdx = otherSubdomain.DofOrderer.globalEnrichedDofs[node, dofType];
                            elementVector[elementDofIdx] = globalFreeVector[globalDofIdx];
                        }
                    }
                    else
                    {
                        foreach (var dofType in enrichment.Dofs)
                        {
                            int elementDofIdx = elementDofs[node, dofType];
                            int globalDofIdx = globalEnrichedDofs[node, dofType];
                            elementVector[elementDofIdx] = globalFreeVector[globalDofIdx];
                        }
                    }
                }
            }

            //foreach (Tuple<XNode2D, EnrichedDof, int> entry in elementDofs)
            //{
            //    int globalEnrichedDof = globalEnrichedDofs[entry.Item1, entry.Item2];
            //    elementVector[entry.Item3] = globalFreeVector[globalEnrichedDof];
            //}
            return Vector.CreateFromArray(elementVector);
        }

        //
        public int GetGlobalEnrichedDofOf(XNode2D node, EnrichedDof dofType)
        {
            return globalEnrichedDofs[node, dofType];
        }

        public int GetSubdomainEnrichedDofOf(XNode2D node, EnrichedDof dofType)
        {
            return subdomainEnrichedDofs[node, dofType];
        }

        public IReadOnlyDictionary<int, int> MatchElementToGlobalEnrichedDofs(XContinuumElement2D element)
        {
            var element2Global = new Dictionary<int, int>();
            int elementDof = 0;
            foreach (XNode2D node in element.Nodes)
            {
                bool isSingularityNode = SingularHeavisideEnrichments.TryGetValue(node,
                    out HashSet<IEnrichmentItem2D> singularEnrichments);

                // TODO: Perhaps the nodal dof types should be decided by the element type (structural, continuum) 
                // and drawn from XXContinuumElement2D instead of from enrichment items
                foreach (IEnrichmentItem2D enrichment in node.EnrichmentItems.Keys)
                {
                    if (isSingularityNode && singularEnrichments.Contains(enrichment))
                    {
                        ++elementDof;
                        continue;
                    }

                    // TODO: Perhaps I could iterate directly on the dofs, ignoring dof types for performance, if the order is guarranteed
                    foreach (EnrichedDof dofType in enrichment.Dofs)
                    {
                        element2Global[elementDof++] = this.globalEnrichedDofs[node, dofType];
                    }
                }
            }
            return element2Global;
        }

        public Dictionary<int, int> MatchElementToSubdomainEnrichedDofs(XContinuumElement2D element)
        {
            var element2Subdomain = new Dictionary<int, int>();
            int elementDof = 0;
            foreach (XNode2D node in element.Nodes)
            {
                bool isSingularityNode = SingularHeavisideEnrichments.TryGetValue(node,
                   out HashSet<IEnrichmentItem2D> singularEnrichments);

                // TODO: Perhaps the nodal dof types should be decided by the element type (structural, continuum) 
                // and drawn from XXContinuumElement2D instead of from enrichment items
                foreach (IEnrichmentItem2D enrichment in node.EnrichmentItems.Keys)
                {
                    if (isSingularityNode && singularEnrichments.Contains(enrichment))
                    {
                        ++elementDof;
                        continue;
                    }

                    // TODO: Perhaps I could iterate directly on the dofs, ignoring dof types for performance, if the order is guarranteed
                    foreach (EnrichedDof dofType in enrichment.Dofs)
                    {
                        element2Subdomain[elementDof++] = this.subdomainEnrichedDofs[node, dofType];
                    }
                }
            }
            return element2Subdomain;
        }

        public void ReorderEnrichedSubdomainDofs(OrderingAMD orderingAlgorithm)
        {
            throw new NotImplementedException();
        }

        public void WriteToConsole()
        {
            Console.WriteLine("Enriched dofs - subdomain order: ");
            Console.WriteLine(subdomainEnrichedDofs);
            Console.WriteLine("Enriched dofs - global order: ");
            Console.WriteLine(globalEnrichedDofs);
        }

        /// <summary>
        /// WARNING: This doesn't work if a node is enriched with more than 1 Heaviside functions from different cracks.
        /// </summary>
        /// <param name="subdomain"></param>
        /// <returns></returns>
        private static Dictionary<XNode2D, HashSet<IEnrichmentItem2D>> FindBoundaryNodesWithSingularHeaviside(
            ICrackDescription crack, XSubdomain2D subdomain)
        {
            var singularNodeEnrichments = new Dictionary<XNode2D, HashSet<IEnrichmentItem2D>>();

            foreach (ISingleCrack singleCrack in crack.SingleCracks)
            {
                // Build nodal supports of boundary Heaviside nodes. 
                // TODO: if the same node is enriched with more than one cracks, then avoid redoing the following for each crack
                var boundaryHeavisideNodes = new List<XNode2D>();
                var nodalSupports = new List<ISet<XContinuumElement2D>>();
                foreach (var node in subdomain.BoundaryNodes)
                {
                    // Only process Heaviside nodes
                    bool isHeaviside = false;
                    foreach (var enrichment in node.EnrichmentItems.Keys)
                    {
                        if (enrichment == singleCrack.CrackBodyEnrichment)
                        {
                            boundaryHeavisideNodes.Add(node);
                            isHeaviside = true;
                            break;
                        }
                    }
                    if (isHeaviside)
                    {
                        // Intersection of nodal support and subdomain
                        var support = new HashSet<XContinuumElement2D>();
                        foreach (var element in singleCrack.Mesh.FindElementsWithNode(node))
                        {
                            if (subdomain.Elements.Contains(element)) support.Add(element);
                        }
                        nodalSupports.Add(support);
                    }

                }

                // Find problematic nodes
                ISet<XNode2D> singularNodes = singleCrack.SingularityResolver.
                    FindHeavisideNodesToRemove(singleCrack, boundaryHeavisideNodes, nodalSupports);

                // Register the problematic nodes
                foreach (var node in singularNodes)
                {
                    bool nodeExists = singularNodeEnrichments.TryGetValue(node, out HashSet<IEnrichmentItem2D> enrichmentsOnly);
                    if (!nodeExists)
                    {
                        enrichmentsOnly = new HashSet<IEnrichmentItem2D>();
                        singularNodeEnrichments.Add(node, enrichmentsOnly);
                    }
                    enrichmentsOnly.Add(singleCrack.CrackBodyEnrichment);
                }
            }

            return singularNodeEnrichments;
        }

        private XSubdomain2D FindSubdomainWhereNodeEnrichmentIsNotSingular(XCluster2D cluster, XNode2D node, 
            IEnrichmentItem2D enrichment)
        {
            foreach (XSubdomain2D subdomain in cluster.Subdomains)
            {
                if (subdomain.BoundaryNodes.Contains(node))
                {
                    bool isSingularityNode = subdomain.DofOrderer.SingularHeavisideEnrichments.TryGetValue(node,
                        out HashSet<IEnrichmentItem2D> singularEnrichments);
                    if (!isSingularityNode) return subdomain;
                    else if (!singularEnrichments.Contains(enrichment)) return subdomain;
                }
            }

            // This point should not be reached under normal circumstances
            if (SingularHeavisideEnrichments.ContainsKey(node)) throw new IncorrectDecompositionException(
                 $"Heaviside dofs of {node} are singular in all subdomains. Investigate further");
            else throw new ArgumentException(
                $"In this subdomain, {node} is not a boundary node with singular Heaviside enrichment");

        }
    }
}
