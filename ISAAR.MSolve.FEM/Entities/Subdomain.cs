﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using ISAAR.MSolve.Discretization.Interfaces;
using ISAAR.MSolve.Numerical.LinearAlgebra;
using ISAAR.MSolve.Numerical.LinearAlgebra.Interfaces;
using System.Globalization;
using System.IO;

namespace ISAAR.MSolve.FEM.Entities
{
    public class Subdomain : ISubdomain
    {
        //private readonly IList<EmbeddedNode> embeddedNodes = new List<EmbeddedNode>();
        private readonly Dictionary<int, Element> elementsDictionary = new Dictionary<int, Element>();
        private readonly Dictionary<int, Node> nodesDictionary = new Dictionary<int, Node>();
        private readonly Dictionary<int, Dictionary<DOFType, int>> nodalDOFsDictionary = new Dictionary<int, Dictionary<DOFType, int>>();
        private readonly Dictionary<int, Dictionary<DOFType, int>> globalNodalDOFsDictionary = new Dictionary<int, Dictionary<DOFType, int>>();
        private readonly Dictionary<int, Dictionary<DOFType, double>> constraintsDictionary = new Dictionary<int, Dictionary<DOFType, double>>();
        private double[] forces;

        #region Properties
        //public IList<EmbeddedNode> EmbeddedNodes
        //{
        //    get { return embeddedNodes; }
        //}

        public Dictionary<int, Element> ElementsDictionary
        {
            get { return elementsDictionary; }
        }

        public Dictionary<int, IElement> ΙElementsDictionary
        {
            get
            {
                var a = new Dictionary<int, IElement>();
                foreach (var element in elementsDictionary.Values)
                    a.Add(element.ID, element);
                return a;
            }
        }

        public Dictionary<int, Node> NodesDictionary
        {
            get { return nodesDictionary; }
        }

        public IList<Node> Nodes
        {
            get { return nodesDictionary.Values.ToList<Node>(); }
        }

        public Dictionary<int, Dictionary<DOFType, double>> Constraints => constraintsDictionary;

        public Dictionary<int, Dictionary<DOFType, int>> NodalDOFsDictionary
        {
            get { return nodalDOFsDictionary; }
        }

        public Dictionary<int, Dictionary<DOFType, int>> GlobalNodalDOFsDictionary
        {
            get { return globalNodalDOFsDictionary; }
        }

        public double[] Forces
        {
            get { return forces; }
        }

        public bool MaterialsModified { get; set; }
        //public bool MaterialsModified
        //{
        //    get
        //    {
        //        bool modified = false;
        //        foreach (Element element in elementsDictionary.Values)
        //            if (element.ElementType.MaterialModified)
        //            {
        //                modified = true;
        //                break;
        //            }
        //        return modified;
        //    }
        //}

        public int ID { get; set; }
        public int TotalDOFs { get; set; }
        #endregion

        #region Data inteconnection routines
        public void EnumerateDOFs()
        {
            TotalDOFs = 0;
            Dictionary<int, List<DOFType>> nodalDOFTypesDictionary = new Dictionary<int, List<DOFType>>();
            foreach (Element element in elementsDictionary.Values)
            {
                for (int i = 0; i < element.Nodes.Count; i++)
                {
                    if (!nodalDOFTypesDictionary.ContainsKey(element.Nodes[i].ID))
                        nodalDOFTypesDictionary.Add(element.Nodes[i].ID, new List<DOFType>());
                    nodalDOFTypesDictionary[element.Nodes[i].ID].AddRange(element.ElementType.DOFEnumerator.GetDOFTypesForDOFEnumeration(element)[i]);
                }
            }

            foreach (Node node in nodesDictionary.Values)
            {
                //List<DOFType> dofTypes = new List<DOFType>();
                //foreach (Element element in node.ElementsDictionary.Values)
                //{
                //    if (elementsDictionary.ContainsKey(element.ID))
                //    {
                //        foreach (DOFType dof in element.ElementType.DOFTypes)
                //            dofTypes.Add(dof);
                //    }
                //}

                Dictionary<DOFType, int> dofsDictionary = new Dictionary<DOFType, int>();
                //foreach (DOFType dofType in dofTypes.Distinct<DOFType>())
                foreach (DOFType dofType in nodalDOFTypesDictionary[node.ID].Distinct<DOFType>())
                {
                    int dofID = 0;
                    #region removeMaria
                    //foreach (DOFType constraint in node.Constraints)
                    //{
                    //    if (constraint == dofType)
                    //    {
                    //        dofID = -1;
                    //        break;
                    //    }
                    //}
                    #endregion

                    foreach (var constraint in node.Constraints)
                    {
                        if (constraint.DOF == dofType)
                        {
                            dofID = -1;
                            break;
                        }
                    }

                    //var embeddedNode = embeddedNodes.Where(x => x.Node == node).FirstOrDefault();
                    ////if (node.EmbeddedInElement != null && node.EmbeddedInElement.ElementType.GetDOFTypes(null)
                    ////    .SelectMany(d => d).Count(d => d == dofType) > 0)
                    ////    dofID = -1;
                    //if (embeddedNode != null && embeddedNode.EmbeddedInElement.ElementType.DOFEnumerator.GetDOFTypes(null)
                    //    .SelectMany(d => d).Count(d => d == dofType) > 0)
                    //    dofID = -1;

                    if (dofID == 0)
                    {
                        dofID = TotalDOFs;
                        TotalDOFs++;
                    }
                    dofsDictionary.Add(dofType, dofID);
                }

                nodalDOFsDictionary.Add(node.ID, dofsDictionary);
            }
            forces = new double[TotalDOFs];
        }

        public void AssignGlobalNodalDOFsFromModel(Dictionary<int, Dictionary<DOFType, int>> glodalDOFsDictionary)
        {
            foreach (int nodeID in nodalDOFsDictionary.Keys)
            {
                Dictionary<DOFType, int> dofTypes = nodalDOFsDictionary[nodeID];
                Dictionary<DOFType, int> globalDOFTypes = new Dictionary<DOFType, int>(dofTypes.Count);
                foreach (DOFType dofType in dofTypes.Keys)
                    globalDOFTypes.Add(dofType, glodalDOFsDictionary[nodeID][dofType]);
                globalNodalDOFsDictionary.Add(nodeID, globalDOFTypes);
            }
        }

        public void BuildNodesDictionary()
        {
            List<int> nodeIDs = new List<int>();
            Dictionary<int, Node> nodes = new Dictionary<int, Node>();
            foreach (Element element in elementsDictionary.Values)
                foreach (Node node in element.Nodes)
                {
                    nodeIDs.Add(node.ID);
                    if (!nodes.ContainsKey(node.ID))
                        nodes.Add(node.ID, node);
                }

            nodeIDs = new List<int>(nodeIDs.Distinct<int>());
            nodeIDs.Sort();
            foreach (int nodeID in nodeIDs)
                nodesDictionary.Add(nodeID, nodes[nodeID]);

            //foreach (var e in modelEmbeddedNodes.Where(x => nodeIDs.IndexOf(x.Node.ID) >= 0))
            //    embeddedNodes.Add(e);
        }

        public void BuildConstraintDisplacementDictionary()
        {
            foreach (Node node in nodesDictionary.Values)
            {
                if (node.Constraints == null) continue;
                constraintsDictionary[node.ID] = new Dictionary<DOFType, double>();
                foreach (Constraint constraint in node.Constraints)
                {
                    constraintsDictionary[node.ID][constraint.DOF] = constraint.Amount;
                }
            }
        }

        #endregion

        public int[] GetCornerNodes()
        {
            int nodex1y1z1 = -1;
            int nodex2y1z1 = -1;
            int nodex1y2z1 = -1;
            int nodex2y2z1 = -1;
            int nodex1y1z2 = -1;
            int nodex2y1z2 = -1;
            int nodex1y2z2 = -1;
            int nodex2y2z2 = -1;
            double x1 = Double.MaxValue;
            double x2 = Double.MinValue;
            double y1 = Double.MaxValue;
            double y2 = Double.MinValue;
            double z1 = Double.MaxValue;
            double z2 = Double.MinValue;

            foreach (var kv in nodesDictionary)
            {
                if (x1 > kv.Value.X) x1 = kv.Value.X;
                if (x2 < kv.Value.X) x2 = kv.Value.X;
                if (y1 > kv.Value.Y) y1 = kv.Value.Y;
                if (y2 < kv.Value.Y) y2 = kv.Value.Y;
                if (z1 > kv.Value.Z) z1 = kv.Value.Z;
                if (z2 < kv.Value.Z) z2 = kv.Value.Z;

                if (x1 == kv.Value.X && y1 == kv.Value.Y && z1 == kv.Value.Z) nodex1y1z1 = kv.Key;
                if (x2 == kv.Value.X && y1 == kv.Value.Y && z1 == kv.Value.Z) nodex2y1z1 = kv.Key;
                if (x1 == kv.Value.X && y2 == kv.Value.Y && z1 == kv.Value.Z) nodex1y2z1 = kv.Key;
                if (x2 == kv.Value.X && y2 == kv.Value.Y && z1 == kv.Value.Z) nodex2y2z1 = kv.Key;
                if (x1 == kv.Value.X && y1 == kv.Value.Y && z2 == kv.Value.Z) nodex1y1z2 = kv.Key;
                if (x2 == kv.Value.X && y1 == kv.Value.Y && z2 == kv.Value.Z) nodex2y1z2 = kv.Key;
                if (x1 == kv.Value.X && y2 == kv.Value.Y && z2 == kv.Value.Z) nodex1y2z2 = kv.Key;
                if (x2 == kv.Value.X && y2 == kv.Value.Y && z2 == kv.Value.Z) nodex2y2z2 = kv.Key;
            }

            return new[] { nodex1y1z1, nodex2y1z1, nodex1y2z1, nodex2y2z1, nodex1y1z2, nodex2y1z2, nodex1y2z2, nodex2y2z2 };
        }

        public void ScaleConstraints(double scalingFactor)
        {
            var nodeIds = constraintsDictionary.Keys.ToList();
            foreach (var nodeId in nodeIds)
            {
                var dofs = constraintsDictionary[nodeId].Keys.ToList();
                foreach (DOFType dof in dofs)
                {
                    constraintsDictionary[nodeId][dof] = constraintsDictionary[nodeId][dof] * scalingFactor;
                }
            }
        }

        public double[] CalculateElementIcrementalConstraintDisplacements(Element element, double constraintScalingFactor)//QUESTION: would it be maybe more clear if we passed the constraintsDictionary as argument??
        {
            int localDOFs = 0;
            foreach (IList<DOFType> dofs in element.ElementType.DOFEnumerator.GetDOFTypes(element)) localDOFs += dofs.Count;
            var elementNodalDisplacements = new double[localDOFs];
            elementNodalDisplacements = ApplyConstraintDisplacements(element, elementNodalDisplacements);
            var incrementalNodalDisplacements = new double[localDOFs];
            elementNodalDisplacements.CopyTo(incrementalNodalDisplacements, 0);
            var icrementalElementNodalDisplacementsVector = new Vector(incrementalNodalDisplacements);
            
            return icrementalElementNodalDisplacementsVector.Data;
        }

        public double[] CalculateElementNodalDisplacements(Element element, IVector globalDisplacementVector)//QUESTION: would it be maybe more clear if we passed the constraintsDictionary as argument??
        {
            double[] elementNodalDisplacements = GetLocalVectorFromGlobal(element, globalDisplacementVector);
            elementNodalDisplacements = ApplyConstraintDisplacements(element, elementNodalDisplacements);
            return elementNodalDisplacements;
        }

        public double[] CalculateElementNodalDisplacements_v2(Element element, LinearAlgebra.Vectors.IVectorView globalDisplacementVector)//QUESTION: would it be maybe more clear if we passed the constraintsDictionary as argument??
        {
            double[] elementNodalDisplacements = GetLocalVectorFromGlobal_v2(element, globalDisplacementVector);
            elementNodalDisplacements = ApplyConstraintDisplacements(element, elementNodalDisplacements);
            return elementNodalDisplacements;
        }

        private double[] ApplyConstraintDisplacements(Element element, double[] elementNodalDisplacements)//QUESTION: should we perhaps make it void??
        {
            int pos = 0;
            for (int i = 0; i < element.ElementType.DOFEnumerator.GetDOFTypes(element).Count; i++)
            {
                INode node = element.ElementType.DOFEnumerator.GetNodesForMatrixAssembly(element)[i]; //Node node = element.Nodes[i];
                foreach (DOFType dofType in element.ElementType.DOFEnumerator.GetDOFTypes(element)[i])
                {
                    Dictionary<DOFType, double> constrainedDOFs;
                    double constraintDisplacement;
                    if (constraintsDictionary.TryGetValue(node.ID, out constrainedDOFs) && constrainedDOFs.TryGetValue(dofType, out constraintDisplacement))
                    {
                        Debug.Assert(elementNodalDisplacements[pos] == 0);
                        elementNodalDisplacements[pos] = constraintDisplacement;
                    }
                    pos++;
                }
            }
            return elementNodalDisplacements;
        }

        public double[] GetLocalVectorFromGlobal(Element element, IVector globalVector)//TODOMaria: here is where the element displacements are assigned to zero if they are restrained
                                                                                       //TODOMaria: Change visibility to private
        {
            int localDOFs = 0;
            foreach (IList<DOFType> dofs in element.ElementType.DOFEnumerator.GetDOFTypes(element)) localDOFs += dofs.Count;
            var localVector = new double[localDOFs];//TODOMaria: here is where I have to check if the dof is constrained

            int pos = 0;
            IList<IList<DOFType>> nodalDofs = element.ElementType.DOFEnumerator.GetDOFTypes(element);
            IList<INode> nodes = element.ElementType.DOFEnumerator.GetNodesForMatrixAssembly(element);
            for (int i = 0; i < nodes.Count; i++)
            {
                foreach (DOFType dofType in nodalDofs[i])
                {
                    int dof = NodalDOFsDictionary[nodes[i].ID][dofType];
                    if (dof != -1) localVector[pos] = globalVector[dof];
                    pos++;
                }
            }
            return localVector;
        }

        //TODO: this should return Vector
        public double[] GetLocalVectorFromGlobal_v2(Element element, LinearAlgebra.Vectors.IVectorView globalVector)
        {
            int localDOFs = 0;
            foreach (IList<DOFType> dofs in element.ElementType.DOFEnumerator.GetDOFTypes(element)) localDOFs += dofs.Count;
            var localVector = new double[localDOFs];

            int pos = 0;
            IList<IList<DOFType>> nodalDofs = element.ElementType.DOFEnumerator.GetDOFTypes(element);
            IList<INode> nodes = element.ElementType.DOFEnumerator.GetNodesForMatrixAssembly(element);
            for (int i = 0; i < nodes.Count; i++)
            {
                foreach (DOFType dofType in nodalDofs[i])
                {
                    int dof = NodalDOFsDictionary[nodes[i].ID][dofType];
                    if (dof != -1) localVector[pos] = globalVector[dof];
                    pos++;
                }
            }
            return localVector;
        }

        public void AddLocalVectorToGlobal(Element element, double[] localVector, double[] globalVector)
        {
            int pos = 0;
            IList<IList<DOFType>> nodalDofs = element.ElementType.DOFEnumerator.GetDOFTypes(element);
            IList<INode> nodes = element.ElementType.DOFEnumerator.GetNodesForMatrixAssembly(element);
            for (int i = 0; i < nodes.Count; i++)
            {
                foreach (DOFType dofType in nodalDofs[i])
                {
                    int dof = NodalDOFsDictionary[nodes[i].ID][dofType];
                    if (dof != -1) globalVector[dof] += localVector[pos];
                    pos++;
                }
            }
        }

        
        public IVector GetRHSFromSolution(IVector solution, IVector dSolution)
        {
           
            var forces = new Vector(TotalDOFs);
            foreach (Element element in elementsDictionary.Values)
            {
                //var localSolution = GetLocalVectorFromGlobal(element, solution);//TODOMaria: This is where the element displacements are calculated //removeMaria
                //var localdSolution = GetLocalVectorFromGlobal(element, dSolution);//removeMaria
                double[] localSolution = CalculateElementNodalDisplacements(element, solution);
                double[] localdSolution = CalculateElementNodalDisplacements(element, dSolution);
                element.ElementType.CalculateStresses(element, localSolution, localdSolution);
                if (element.ElementType.MaterialModified)
                    element.Subdomain.MaterialsModified = true;
                double[] f = element.ElementType.CalculateForces(element, localSolution, localdSolution);
                AddLocalVectorToGlobal(element, f, forces.Data);
            }
            return forces;
        }

        public LinearAlgebra.Vectors.IVector GetRHSFromSolution_v2(LinearAlgebra.Vectors.IVectorView solution, LinearAlgebra.Vectors.IVectorView dSolution)
        {
            var forces = new double[TotalDOFs]; //TODO: use Vector
            foreach (Element element in elementsDictionary.Values)
            {
                //var localSolution = GetLocalVectorFromGlobal(element, solution);//TODOMaria: This is where the element displacements are calculated //removeMaria
                //var localdSolution = GetLocalVectorFromGlobal(element, dSolution);//removeMaria
                double[] localSolution = CalculateElementNodalDisplacements_v2(element, solution);
                double[] localdSolution = CalculateElementNodalDisplacements_v2(element, dSolution);
                element.ElementType.CalculateStresses(element, localSolution, localdSolution);
                if (element.ElementType.MaterialModified)
                    element.Subdomain.MaterialsModified = true;
                double[] f = element.ElementType.CalculateForces(element, localSolution, localdSolution);
                AddLocalVectorToGlobal(element, f, forces);
            }
            return LinearAlgebra.Vectors.Vector.CreateFromArray(forces);
        }

        public void SaveMaterialState()
        {
            foreach (Element element in elementsDictionary.Values) element.ElementType.SaveMaterialState();
        }

        public void ResetMaterialsModifiedProperty()
        {
            this.MaterialsModified = false;
            foreach (Element element in elementsDictionary.Values) element.ElementType.ResetMaterialModified();
        }

        public void ClearMaterialStresses()
        {
            foreach (Element element in elementsDictionary.Values) element.ElementType.ClearMaterialStresses();
        }

        public void SubdomainToGlobalVector(double[] vIn, double[] vOut)
        {
            foreach (int nodeID in GlobalNodalDOFsDictionary.Keys)
            {
                Dictionary<DOFType, int> dofTypes = NodalDOFsDictionary[nodeID];
                foreach (DOFType dofType in dofTypes.Keys)
                {
                    int localDOF = NodalDOFsDictionary[nodeID][dofType];
                    int globalDOF = GlobalNodalDOFsDictionary[nodeID][dofType];
                    if (localDOF > -1 && globalDOF > -1) vOut[globalDOF] += vIn[localDOF];
                }
            }
        }

        public void SubdomainToGlobalVector_v2(LinearAlgebra.Vectors.IVectorView subdomainVector, LinearAlgebra.Vectors.IVector globalVector)
        {
            foreach (int nodeID in GlobalNodalDOFsDictionary.Keys)
            {
                Dictionary<DOFType, int> dofTypes = NodalDOFsDictionary[nodeID];
                foreach (DOFType dofType in dofTypes.Keys)
                {
                    int localDOF = NodalDOFsDictionary[nodeID][dofType];
                    int globalDOF = GlobalNodalDOFsDictionary[nodeID][dofType];
                    if (localDOF > -1 && globalDOF > -1)
                    {
                        //TODO: add a Vector.SetSubvector and Vector.AddSubvector for incontiguous entries
                        globalVector.Set(globalDOF, globalVector[globalDOF] + subdomainVector[localDOF]);
                    }
                }
            }
        }

        public void SubdomainToGlobalVectorMeanValue(double[] vIn, double[] vOut)
        {
            throw new NotImplementedException();
        }

        public void SubdomainToGlobalVectorMeanValue_v2(LinearAlgebra.Vectors.IVectorView globalVector, LinearAlgebra.Vectors.IVector subdomainVector)
        {
            throw new NotImplementedException();
        }

        public void SplitGlobalVectorToSubdomain(double[] vIn, double[] vOut)
        {
            foreach (int nodeID in GlobalNodalDOFsDictionary.Keys)
            {
                Dictionary<DOFType, int> dofTypes = NodalDOFsDictionary[nodeID];
                foreach (DOFType dofType in dofTypes.Keys)
                {
                    int localDOF = NodalDOFsDictionary[nodeID][dofType];
                    int globalDOF = GlobalNodalDOFsDictionary[nodeID][dofType];
                    if (localDOF > -1 && globalDOF > -1) vOut[localDOF] = vIn[globalDOF];
                }
            }
        }

        public void SplitGlobalVectorToSubdomain_v2(LinearAlgebra.Vectors.IVectorView globalVector, LinearAlgebra.Vectors.IVector subdomainVector)
        {
            foreach (int nodeID in GlobalNodalDOFsDictionary.Keys)
            {
                Dictionary<DOFType, int> dofTypes = NodalDOFsDictionary[nodeID];
                foreach (DOFType dofType in dofTypes.Keys)
                {
                    int localDOF = NodalDOFsDictionary[nodeID][dofType];
                    int globalDOF = GlobalNodalDOFsDictionary[nodeID][dofType];
                    if (localDOF > -1 && globalDOF > -1)
                    {
                        //TODO: add a Vector.SetSubvector and Vector.AddSubvector for incontiguous entries
                        subdomainVector.Set(localDOF, globalVector[globalDOF]);
                    }
                }
            }
        }
    }
}
