using System.Reflection;
using McpCad.Core.Modeling;

namespace McpCad.Tests.Modeling;

public class PartModelingContractsTests
{
    [Fact]
    public void Contracts_ExposeClosedEnumsTwoOperationResultAndNativeFreeReferences()
    {
        Enum.GetValues<CanonicalPlane>().Should().Equal(
            CanonicalPlane.Top, CanonicalPlane.Front, CanonicalPlane.Right);
        Enum.GetValues<LengthUnit>().Should().Equal(
            LengthUnit.Millimeter, LengthUnit.Centimeter, LengthUnit.Meter);
        Enum.GetValues<ExtrusionDirection>().Should().Equal(
            ExtrusionDirection.Positive, ExtrusionDirection.Negative);
        typeof(PartModelingResult).GetProperties().Select(property => property.Name)
            .Should().BeEquivalentTo("Sketch", "Extrusion");
        typeof(IPartModelingContext).GetProperty(nameof(IPartModelingContext.Capabilities)).Should().NotBeNull();
        PublicMembersAcceptingNativeData(typeof(SketchReference)).Should().BeEmpty();
        PublicMembersAcceptingNativeData(typeof(FeatureReference)).Should().BeEmpty();
    }

    [Fact]
    public void PortableValuesAreInitOnlyAndReferencesExposeOnlyNativeFreeFactories()
    {
        typeof(Point2).GetProperty(nameof(Point2.X))!.SetMethod!.ReturnParameter
            .GetRequiredCustomModifiers().Should().Contain(typeof(System.Runtime.CompilerServices.IsExternalInit));
        foreach (var type in new[] { typeof(SketchReference), typeof(FeatureReference) })
        {
            type.GetConstructors().Should().BeEmpty();
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Should().BeEmpty();
            type.GetMethod("Create", BindingFlags.Public | BindingFlags.Static)!.GetParameters().Should().BeEmpty();
        }
    }

    private static IEnumerable<MemberInfo> PublicMembersAcceptingNativeData(Type type)
    {
        return type.GetConstructors()
            .Cast<MemberInfo>()
            .Concat(type.GetProperties())
            .Where(member => member.Name.Contains("id", StringComparison.OrdinalIgnoreCase)
                || member.Name.Contains("address", StringComparison.OrdinalIgnoreCase)
                || member.Name.Contains("target", StringComparison.OrdinalIgnoreCase)
                || member.Name.Contains("revision", StringComparison.OrdinalIgnoreCase)
                || member.Name.Contains("evidence", StringComparison.OrdinalIgnoreCase));
    }
}
