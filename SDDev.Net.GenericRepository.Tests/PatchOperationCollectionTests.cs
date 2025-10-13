using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SDDev.Net.GenericRepository.Contracts.BaseEntity;
using SDDev.Net.GenericRepository.CosmosDB.Patch.AzureSearch;
using SDDev.Net.GenericRepository.CosmosDB.Patch.Cosmos;
using SDDev.Net.GenericRepository.Tests.TestModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SDDev.Net.GenericRepository.Tests;

[TestClass]
public class PatchOperationCollectionTests
{
    [TestMethod]
    public void WhenBuildingCosmosPatchOperations_ThenExpectedOperationsAreBuilt()
    {
        // Arrange
        var testObject = new TestAuditableObject
        {
            Collection = new List<string> { "Test1", "Test2", "Test3" },
            Number = 1,
            Prop1 = "Test",
            ChildObject = new TestObject { Prop1 = "Test" },
            ExampleProperty = "Test",
            UUID = Guid.NewGuid(),
        };

        var auditMetadata = new AuditMetadata
        {
            CreatedBy = Guid.NewGuid(),
            CreatedDateTime = DateTime.Now,
        };

        var patchOperationCollection = new CosmosPatchOperationCollection<TestAuditableObject>();

        // Act
        patchOperationCollection.Add(x => x.AuditMetadata, auditMetadata);
        patchOperationCollection.Remove(x => x.Collection[0]);
        patchOperationCollection.Remove(x => x.ExampleProperty);
        patchOperationCollection.Replace(x => x.Collection[1], "Test4");
        patchOperationCollection.Set(x => x.Collection[2], "Test5");
        patchOperationCollection.Set(x => x.ChildObject.ExampleProperty, "Test6");
        patchOperationCollection.Add(x => x.Collection, "Test7");

        var operations = patchOperationCollection.ToList();

        // Assert
        Assert.AreEqual(7, operations.Count);

        var addMetadataOperation = operations.Single(x => x.Path == "/AuditMetadata");
        addMetadataOperation.Value.Should().Be(auditMetadata);
        addMetadataOperation.ToOperation<PatchOperation>().OperationType.Should().Be(PatchOperationType.Add);

        var removeOperation = operations.Single(x => x.Path == "/Collection/0");
        removeOperation.Value.Should().Be(null);
        removeOperation.ToOperation<PatchOperation>().OperationType.Should().Be(PatchOperationType.Remove);

        var remoteExamplePropertyOperation = operations.Single(x => x.Path == "/ExampleProperty");
        remoteExamplePropertyOperation.Value.Should().Be(null);
        remoteExamplePropertyOperation.ToOperation<PatchOperation>().OperationType.Should().Be(PatchOperationType.Remove);

        var replaceOperation = operations.Single(x => x.Path == "/Collection/1");
        replaceOperation.Value.Should().Be("Test4");
        replaceOperation.ToOperation<PatchOperation>().OperationType.Should().Be(PatchOperationType.Replace);

        var setOperation = operations.First(x => x.Path == "/Collection/2");
        setOperation.Value.Should().Be("Test5");
        setOperation.ToOperation<PatchOperation>().OperationType.Should().Be(PatchOperationType.Set);

        var setChildObjectOperation = operations.First(x => x.Path == "/ChildObject/ExampleProperty");
        setChildObjectOperation.Value.Should().Be("Test6");
        setChildObjectOperation.ToOperation<PatchOperation>().OperationType.Should().Be(PatchOperationType.Set);

        var addCollectionOperation = operations.First(x => x.Path == "/Collection/-");
        addCollectionOperation.Value.Should().Be("Test7");
        addCollectionOperation.ToOperation<PatchOperation>().OperationType.Should().Be(PatchOperationType.Add);
    }

    [TestMethod]
    public void WhenbuildingAzureSearchPatchOperations_ThenExpectedOperationsArebuilt()
    {
        // Arrange
        var testObject = new TestAuditableObject
        {
            Collection = new List<string> { "Test1", "Test2", "Test3" },
            Number = 1,
            Prop1 = "Test",
            ChildObject = new TestObject { Prop1 = "Test" },
            ExampleProperty = "Test",
            UUID = Guid.NewGuid(),
        };

        var createdById = Guid.NewGuid();

        var patchOperationCollection = new AzureSearchPatchOperationCollection<TestAuditableObject>();

        // Act
        patchOperationCollection.Add(x => x.AuditMetadata.CreatedBy, createdById);

        var operations = patchOperationCollection.ToDictionary(x => x.Path, x => x.Value);

        // Assert
        operations.Should().HaveCount(2); // We have two operations, one for Azure Search and one for Cosmos
        operations["AuditMetadata.CreatedBy"].Should().Be(createdById);
    }
}
