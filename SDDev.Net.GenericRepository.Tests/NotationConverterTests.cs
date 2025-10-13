using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SDDev.Net.GenericRepository.CosmosDB.Patch.Converters;
using SDDev.Net.GenericRepository.Tests.TestModels;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace SDDev.Net.GenericRepository.Tests;

[TestClass]
public class NotationConverterTests
{
    [TestMethod]
    public async Task WhenPassingAnExpression_ThenTheExpectedSlashNotationIsCreated()
    {
        // ARRANGE

        var testObjectConverter = new SlashNotationConverter<TestObject>();
        var complexTestObjectConverter = new SlashNotationConverter<ComplexTestObject>();
        var testAuditableObjectConverter = new SlashNotationConverter<TestAuditableObject>();

        // ACT / Assert

        // Simple Properties
        var simpleStringField = (Expression<Func<TestObject, string>>)(x => x.Prop1);
        testObjectConverter.ConvertToPath(simpleStringField).Should().Be("/Prop1");
        var simpleGuidField = (Expression<Func<TestObject, Guid?>>)(x => x.UUID);
        testObjectConverter.ConvertToPath(simpleGuidField).Should().Be("/UUID");
        var simpleNumberField = (Expression<Func<TestObject, int>>)(x => x.Number);
        testObjectConverter.ConvertToPath(simpleNumberField).Should().Be("/Number");

        // Array Properties
        var arrayOfStringsField = (Expression<Func<TestObject, IEnumerable<string>>>)(x => x.Collection);
        testObjectConverter.ConvertToPath(arrayOfStringsField).Should().Be("/Collection/-");
        var arraySimpleIndexField = (Expression<Func<TestObject, string>>)(x => x.Collection[2]);
        testObjectConverter.ConvertToPath(arraySimpleIndexField).Should().Be("/Collection/2");
        var arrayOfComplexField = (Expression<Func<ComplexTestObject, IEnumerable<TestAuditableObject>>>)(x => x.Complex);
        complexTestObjectConverter.ConvertToPath(arrayOfComplexField).Should().Be("/Complex/-");
        var arrayComplexIndexField = (Expression<Func<ComplexTestObject, TestAuditableObject>>)(x => x.Complex[0]);
        complexTestObjectConverter.ConvertToPath(arrayComplexIndexField).Should().Be("/Complex/0");

        // Nested Simple Properties
        var simpleNestedStringField = (Expression<Func<TestObject, string>>)(x => x.ChildObject.Prop1);
        testObjectConverter.ConvertToPath(simpleNestedStringField).Should().Be("/ChildObject/Prop1");
        var simpleNestedGuidField = (Expression<Func<TestObject, Guid?>>)(x => x.ChildObject.UUID);
        testObjectConverter.ConvertToPath(simpleNestedGuidField).Should().Be("/ChildObject/UUID");
        var simpleNestedDateTimeField = (Expression<Func<TestAuditableObject, DateTime?>>)(x => x.AuditMetadata.ModifiedDateTime);
        testAuditableObjectConverter.ConvertToPath(simpleNestedDateTimeField).Should().Be("/AuditMetadata/ModifiedDateTime");

        // Nested Array Properties
        var nestedArrayOfStringsField = (Expression<Func<TestObject, IEnumerable<string>>>)(x => x.ChildObject.Collection);
        testObjectConverter.ConvertToPath(nestedArrayOfStringsField).Should().Be("/ChildObject/Collection/-");
        var nestedArraySimpleIndexField = (Expression<Func<TestObject, string>>)(x => x.ChildObject.Collection[2]);
        testObjectConverter.ConvertToPath(nestedArraySimpleIndexField).Should().Be("/ChildObject/Collection/2");
        var nestedArrayOfComplexField = (Expression<Func<ComplexTestObject, string>>)(x => x.Complex[0].Prop1);
        complexTestObjectConverter.ConvertToPath(nestedArrayOfComplexField).Should().Be("/Complex/0/Prop1");
        var nestedArrayComplexIndexField = (Expression<Func<ComplexTestObject, TestObject>>)(x => x.Complex[0].ChildObject);
        complexTestObjectConverter.ConvertToPath(nestedArrayComplexIndexField).Should().Be("/Complex/0/ChildObject");
        var nestedArrayJsonField = (Expression<Func<ComplexTestObject, string>>)(x => x.Complexities[2].ComplexString);
        complexTestObjectConverter.ConvertToPath(nestedArrayJsonField).Should().Be("/Complexities/2/complexString");
        var nestedArrayJsonField2 = (Expression<Func<ComplexTestObject, string>>)(x => x.Complexities[2].Complexity_String);
        complexTestObjectConverter.ConvertToPath(nestedArrayJsonField2).Should().Be("/Complexities/2/complexity_String");

        // Super Complex
        var superComplexField = (Expression<Func<ComplexTestObject, string>>)(x => x.Complex[0].ChildObject.Collection[2]);
        complexTestObjectConverter.ConvertToPath(superComplexField).Should().Be("/Complex/0/ChildObject/Collection/2");
        var superComplexNestedField = (Expression<Func<ComplexTestObject, string>>)(x => x.Complex[0].ChildObject.ChildObject.Prop1);
        complexTestObjectConverter.ConvertToPath(superComplexNestedField).Should().Be("/Complex/0/ChildObject/ChildObject/Prop1");
        var superSuperComplex = (Expression<Func<ComplexTestObject, IEnumerable<string>>>)(x => x.Complexities[2].Complex[5].ChildObject.ChildObject.ChildObject.Collection);
        complexTestObjectConverter.ConvertToPath(superSuperComplex).Should().Be("/Complexities/2/Complex/5/ChildObject/ChildObject/ChildObject/Collection/-");
    }

    [TestMethod]
    public async Task WhenPassingAnExpression_ThenTheExpectedDotNotationIsCreated()
    {
        // ARRANGE

        var testObjectConverter = new DotNotationConverter<TestObject>();
        var complexTestObjectConverter = new DotNotationConverter<ComplexTestObject>();
        var testAuditableObjectConverter = new DotNotationConverter<TestAuditableObject>();

        // ACT / ASSERT

        // Simple Properties
        var simpleStringField = (Expression<Func<TestObject, string>>)(x => x.Prop1);
        testObjectConverter.ConvertToPath(simpleStringField).Should().Be("Prop1");
        var simpleGuidField = (Expression<Func<TestObject, Guid?>>)(x => x.UUID);
        testObjectConverter.ConvertToPath(simpleGuidField).Should().Be("UUID");
        var simpleNumberField = (Expression<Func<TestObject, int>>)(x => x.Number);
        testObjectConverter.ConvertToPath(simpleNumberField).Should().Be("Number");

        // Array Properties
        var arrayOfStringsField = (Expression<Func<TestObject, IEnumerable<string>>>)(x => x.Collection);
        testObjectConverter.ConvertToPath(arrayOfStringsField).Should().Be("Collection");
        var arrayOfComplexField = (Expression<Func<ComplexTestObject, IEnumerable<TestAuditableObject>>>)(x => x.Complex);
        complexTestObjectConverter.ConvertToPath(arrayOfComplexField).Should().Be("Complex");

        // Nested Simple Properties
        var simpleNestedStringField = (Expression<Func<TestObject, string>>)(x => x.ChildObject.Prop1);
        testObjectConverter.ConvertToPath(simpleNestedStringField).Should().Be("ChildObject.Prop1");
        var simpleNestedGuidField = (Expression<Func<TestObject, Guid?>>)(x => x.ChildObject.UUID);
        testObjectConverter.ConvertToPath(simpleNestedGuidField).Should().Be("ChildObject.UUID");
        var simpleNestedDateTimeField = (Expression<Func<TestAuditableObject, DateTime?>>)(x => x.AuditMetadata.ModifiedDateTime);
        testAuditableObjectConverter.ConvertToPath(simpleNestedDateTimeField).Should().Be("AuditMetadata.ModifiedDateTime");

        // Nested Array Properties
        var nestedArrayOfStringsField = (Expression<Func<TestObject, IEnumerable<string>>>)(x => x.ChildObject.Collection);
        testObjectConverter.ConvertToPath(nestedArrayOfStringsField).Should().Be("ChildObject.Collection");

        // Super Nested
        var superNestedField = (Expression<Func<TestObject, IEnumerable<string>>>)(x => x.ChildObject.ChildObject.ChildObject.Collection);
        testObjectConverter.ConvertToPath(superNestedField).Should().Be("ChildObject.ChildObject.ChildObject.Collection");
    }
}
