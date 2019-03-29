﻿using ISAAR.MSolve.LinearAlgebra.Vectors;
using ISAAR.MSolve.MultiscaleAnalysis.SupportiveClasses;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace ISAAR.MSolve.Tests.FEMpartB
{
    public static class MicrostructureBvpNRNLAnalyzerTest
    {
        [Fact]
        public static void CheckMicrostructureBvpNRNLAnalyzer()
        {
            string results_file1 = "..\\..\\..\\InputFiles\\MSmicroBvpAnalyzerTest\\U_sunol_micro_3.txt";
            string results_file2 = "..\\..\\..\\InputFiles\\MSmicroBvpAnalyzerTest\\U_sunol_micro_6.txt";
            double[] displacements1sIncrement = PrintUtilities.ReadVector(results_file1);
            double[] displacements2ndncrement = PrintUtilities.ReadVector(results_file2);

            (IVector uInitialFreeDOFs_state1, IVector uInitialFreeDOFs_state2) = NRNLAnalyzerDevelopTest_v2.SolveDisplLoadsExample();

            
            Assert.True(NRNLAnalyzerDevelopTest_v2.AreDisplacementsSame_v2(displacements1sIncrement, uInitialFreeDOFs_state1.CopyToArray()));
            Assert.True(NRNLAnalyzerDevelopTest_v2.AreDisplacementsSame_v2(displacements2ndncrement, uInitialFreeDOFs_state2.CopyToArray()));
        }
    }
}
