using Loom.Caveats;

namespace Loom.Tests;

public class CaveatParserAndEvaluatorTests
{
    [Fact]
    public void Evaluate_InListCaveat_SatisfiedWhenRegionMatches()
    {
        var caveats = CaveatParser.Parse(
            """
            caveat match_region(expected_regions list<string>, observed_region string) {
              observed_region in expected_regions
            }
            """);

        var definition = caveats["match_region"];

        var context = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["expected_regions"] = new List<string> { "eu-west-1", "us-east-1" },
            ["observed_region"] = "eu-west-1",
        };

        var result = CaveatEvaluator.Evaluate(definition, context);

        var satisfied = Assert.IsType<CaveatEvalResult.Satisfied>(result);
        Assert.True(satisfied.Value);
    }

    [Fact]
    public void Evaluate_InListCaveat_NotSatisfiedWhenRegionDoesNotMatch()
    {
        var caveats = CaveatParser.Parse(
            """
            caveat match_region(expected_regions list<string>, observed_region string) {
              observed_region in expected_regions
            }
            """);

        var definition = caveats["match_region"];

        var context = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["expected_regions"] = new List<string> { "eu-west-1" },
            ["observed_region"] = "ap-south-1",
        };

        var result = CaveatEvaluator.Evaluate(definition, context);

        var satisfied = Assert.IsType<CaveatEvalResult.Satisfied>(result);
        Assert.False(satisfied.Value);
    }

    [Fact]
    public void Evaluate_MissingRequiredVariable_ReturnsMissingContext()
    {
        var caveats = CaveatParser.Parse(
            """
            caveat match_region(expected_regions list<string>, observed_region string) {
              observed_region in expected_regions
            }
            """);

        var definition = caveats["match_region"];

        var context = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["expected_regions"] = new List<string> { "eu-west-1" },
        };

        var result = CaveatEvaluator.Evaluate(definition, context);

        var missing = Assert.IsType<CaveatEvalResult.MissingContext>(result);
        Assert.Equal(["observed_region"], missing.MissingVariables);
    }

    [Fact]
    public void Evaluate_NumericComparisonAndConjunction()
    {
        var caveats = CaveatParser.Parse(
            """
            caveat min_amount(amount int, threshold int, is_active bool) {
              amount >= threshold && is_active
            }
            """);

        var definition = caveats["min_amount"];

        var context = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["amount"] = 500,
            ["threshold"] = 100,
            ["is_active"] = true,
        };

        var result = CaveatEvaluator.Evaluate(definition, context);

        var satisfied = Assert.IsType<CaveatEvalResult.Satisfied>(result);
        Assert.True(satisfied.Value);
    }

    [Fact]
    public void Evaluate_CrossNumericTypeComparison_TreatsIntAndLongAsEqual()
    {
        var caveats = CaveatParser.Parse(
            """
            caveat min_amount(amount int, threshold int) {
              amount >= threshold
            }
            """);

        var definition = caveats["min_amount"];

        var context = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["amount"] = 500L,
            ["threshold"] = 500L,
        };

        var result = CaveatEvaluator.Evaluate(definition, context);

        var satisfied = Assert.IsType<CaveatEvalResult.Satisfied>(result);
        Assert.True(satisfied.Value);
    }
}
