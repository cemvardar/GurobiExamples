using System.Collections.Generic;
using NUnit.Framework;

namespace DietSolver
{
    [TestFixture]
    public class DietSolverTests
    {
        [Test]
        public void ChacterizationTest()
        {
            var consoleWriter = new OutputWriteMock();
            var dietSolver = new DietSolver(consoleWriter);
            dietSolver.BuildAndSolveDietLp();
            consoleWriter.Verify();
        }
    }
    public class OutputWriteMock :IOutputWriter
    {
        private List<string> lines = new List<string>();
        public void WriteLine(string text)
        {
            lines.Add(text);
        }
        public void Verify()
        {
            Assert.AreEqual(lines[0], "\nCost: 11.8288611111111");
            Assert.AreEqual(lines[1], "\nBuy:");
            Assert.AreEqual(lines[2], "hamburger 0.604513888888889");
            Assert.AreEqual(lines[3], "milk 6.97013888888889");
            Assert.AreEqual(lines[4], "ice cream 2.59131944444444");
            Assert.AreEqual(lines[5], "\nNutrition:");
            Assert.AreEqual(lines[6], "calories 1800");
            Assert.AreEqual(lines[7], "protein 91");
            Assert.AreEqual(lines[8], "fat 59.0559027777778");
            Assert.AreEqual(lines[9], "sodium 1779");
            Assert.AreEqual(lines[10], "\nAdding constraint: at most 6 servings of dairy");
            Assert.AreEqual(lines[11], "No solution");
        }
    }
}