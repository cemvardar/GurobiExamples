using System;
using System.Collections.Generic;
using System.Linq;
using Gurobi;

namespace DietSolver
{
    public class DietSolverConsole
    {
        public static void Main()
        {
            try
            {
                new DietSolver(new ConsoleWriter()).BuildAndSolveDietLp();
            }
            catch (GRBException e)
            {
                Console.WriteLine("Error code: " + e.ErrorCode + ". " + e.Message);
            }
        }
    }

    public class DietLpProblem
    {
        public string[] categories;
        public int NumberOfCategories;
        public double[] minNutrition;
        public double[] maxNutrition;
        public string[] foodTypes;
        public int numberOfFoodTypes;
        public double[] cost;
        public double[,] nutritionValues;

        public DecisionVariableSet nutritionDecisionVariables;
        public DecisionVariableSet boughtAmount;

        public DietLpProblem()
        {
            SetupDietLpData();
        }

        private void SetupDietLpData()
        {
            // Nutrition guidelines, based on
            // USDA Dietary Guidelines for Americans, 2005
            // http://www.health.gov/DietaryGuidelines/dga2005/
            categories = new[] { "calories", "protein", "fat", "sodium" };
            NumberOfCategories = categories.Length;

            minNutrition = new[] { 1800, 91, 0, 0.0 };
            maxNutrition = new[] { 2200, GRB.INFINITY, 65, 1779 };

            // Set of foodTypes
            foodTypes = new[]
                            {
                                "hamburger", "chicken", "hot dog", "fries",
                                "macaroni", "pizza", "salad", "milk", "ice cream"
                            };
            numberOfFoodTypes = foodTypes.Length;
            cost = new[]
                       {
                           2.49, 2.89, 1.50, 1.89, 2.09, 1.99, 2.49, 0.89,
                           1.59
                       };

            // Nutrition values for the foodTypes
            nutritionValues = new[,]
                                  {
                                      {410, 24, 26, 730}, // hamburger
                                      {420, 32, 10, 1190}, // chicken
                                      {560, 20, 32, 1800}, // hot dog
                                      {380, 4, 19, 270}, // fries
                                      {320, 12, 10, 930}, // macaroni
                                      {320, 15, 12, 820}, // pizza
                                      {320, 31, 12, 1230}, // salad
                                      {100, 8, 2.5, 125}, // milk
                                      {330, 8, 10, 180} // ice cream
                                  };
        }
        public IEnumerable<DecisionVariableData> GetNutritionDecisionVariables()
        {
            List<DecisionVariableData> retList = new List<DecisionVariableData>();
            for (int i = 0; i < NumberOfCategories; ++i)
            {
                retList.Add(DecisionVariableData.GetContinousDecisionVariable(minNutrition[i], maxNutrition[i],0,categories[i]));
            }
            return retList;
        }
        public IEnumerable<DecisionVariableData> GetBoughtAmountDecisionVariables()
        {
            List<DecisionVariableData> retList = new List<DecisionVariableData>();
            for (int i = 0; i < numberOfFoodTypes; ++i)
            {
                retList.Add(DecisionVariableData.GetContinousDecisionVariable(0, GRB.INFINITY, cost[i], foodTypes[i]));
            }
            return retList;
        }

        public void AddNutritionConstraints(GRBModel model)
        {
            // Nutrition constraints
            for (int i = 0; i < NumberOfCategories; ++i)
            {
                GRBLinExpr ntot = 0.0;
                for (int j = 0; j < numberOfFoodTypes; ++j)
                    ntot += nutritionValues[j, i] * boughtAmount[j];
                model.AddConstr(ntot == nutritionDecisionVariables[i], categories[i]);
            }
        }

        public void SetupBuyDecisionVariables(GRBModel model)
        {
            boughtAmount = new DecisionVariableSet(model, GetBoughtAmountDecisionVariables(), "Buy");
        }

        public void SetupNutritionDecisionVariables(GRBModel model)
        {
            nutritionDecisionVariables = new DecisionVariableSet(model, GetNutritionDecisionVariables(), "Nutrition");
        }

        public void AddExtraConstraint(GRBModel model)
        {
            model.AddConstr(boughtAmount[7] + boughtAmount[8] <= 6.0, "limit_dairy");
        }

        public void SetupDecisionVariables(GRBModel model)
        {
            // Create decision variables for the nutrition information,
            // which we limit via bounds
            SetupNutritionDecisionVariables(model);
            // Create decision variables for the foodTypes to buy
            SetupBuyDecisionVariables(model);
        }

        public void WriteDecisionVaribleValues(IOutputWriter writer)
        {
            boughtAmount.PrintToConsole(writer);
            nutritionDecisionVariables.PrintToConsole(writer);
        }
    }

    public class DecisionVariableData
    {
        private readonly double lowerBound;
        private readonly double upperBound;
        private readonly double ObjectiveCoefficient;
        private readonly Char variableType;
        private readonly string name;

        public static DecisionVariableData GetContinousDecisionVariable(double lowerBound, double upperBound,
                                                                    double objectiveCoefficient, string name)
        {
            return new DecisionVariableData(lowerBound, upperBound, objectiveCoefficient, GRB.CONTINUOUS, name);
        }

        private DecisionVariableData(double lowerBound, double upperBound, double objectiveCoefficient, char variableType,
                                string name)
        {
            this.lowerBound = lowerBound;
            this.upperBound = upperBound;
            ObjectiveCoefficient = objectiveCoefficient;
            this.variableType = variableType;
            this.name = name;
        }

        public GRBVar AddToModel(GRBModel model)
        {
            return model.AddVar(lowerBound, upperBound, ObjectiveCoefficient, variableType, name);
        }
    }
    
    public class DietSolver
    {
        private readonly IOutputWriter _writer;
        private DietLpProblem DietLpProblem;
        
        private GRBEnv env;
        private GRBModel model;

        public DietSolver(IOutputWriter writer)
        {
            _writer = writer;
        }

        public void BuildAndSolveDietLp()
        {
            DietLpProblem = new DietLpProblem();
            
            SetupGurobiEnvironmentAndModel();

            DietLpProblem.SetupDecisionVariables(model);

            // The objective is to minimize the costs
            model.Set(GRB.IntAttr.ModelSense, 1);
            // Update model to integrate new variables
            model.Update();

            DietLpProblem.AddNutritionConstraints(model);

            OptimizeAndPrintSolution();

            AddLimitDiaryConstraint();

            OptimizeAndPrintSolution();

            DisposeModelAndEnvironment();
        }

        private void DisposeModelAndEnvironment()
        {
            // Dispose of model and env
            model.Dispose();
            env.Dispose();
        }

        private void AddLimitDiaryConstraint()
        {
            _writer.WriteLine("\nAdding constraint: at most 6 servings of dairy");
            DietLpProblem.AddExtraConstraint(model);
        }

        private void OptimizeAndPrintSolution()
        {
            // Solve
            model.Optimize();
            PrintSolution();
        }

        private void SetupGurobiEnvironmentAndModel()
        {
            // Model
            env = new GRBEnv();
            model = new GRBModel(env);
            model.Set(GRB.StringAttr.ModelName, "diet");
        }

        private void PrintSolution()
        {
            if (model.Get(GRB.IntAttr.Status) == GRB.Status.OPTIMAL)
            {
                _writer.WriteLine("\nCost: " + model.Get(GRB.DoubleAttr.ObjVal));
                DietLpProblem.WriteDecisionVaribleValues(_writer);
            }
            else
            {
                _writer.WriteLine("No solution");
            }
        }
    }

    public interface IOutputWriter
    {
        void WriteLine(string text);
    }

    public class ConsoleWriter : IOutputWriter
    {
        public void WriteLine(string text)
        {
            Console.WriteLine(text);
        }
    }
    public class DecisionVariableSet
    {
        private GRBVar[] decisionVariables;
        private string _label;

        public DecisionVariableSet(GRBModel model, IEnumerable<DecisionVariableData> decisionVariableData, string label)
        {
            // Create decision variables for the nutrition information,
            // which we limit via bounds
            NumberOfDecisonVariables = decisionVariableData.Count();
            decisionVariables = new GRBVar[NumberOfDecisonVariables];
            int i = 0;
            foreach (var dv in decisionVariableData)
            {
                decisionVariables[i] = dv.AddToModel(model);
                i++;
            }
            _label = label;
        }

        public int NumberOfDecisonVariables { get; private set; }

        public GRBVar this[int i]
        {
            get { return decisionVariables[i]; }
        }

        public void PrintToConsole(IOutputWriter writer)
        {
            writer.WriteLine("\n" + _label + ":");
            for (int j = 0; j < NumberOfDecisonVariables; ++j)
            {
                if (this[j].Get(GRB.DoubleAttr.X) > 0.0001)
                {
                    writer.WriteLine(this[j].Get(GRB.StringAttr.VarName) + " " +
                        this[j].Get(GRB.DoubleAttr.X));
                }
            }
        }
    }

}