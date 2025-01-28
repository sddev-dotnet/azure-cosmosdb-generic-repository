using SDDev.Net.GenericRepository.Contracts.BaseEntity;
using System;
using System.Collections.Generic;

namespace SDDev.Net.GenericRepository.Tests.TestModels;

public class TestAuditableObject : BaseAuditableEntity
{
    public List<string> Collection { get; set; }

    public int Number { get; set; }

    public string Prop1 { get; set; }

    public TestObject ChildObject { get; set; }

    public string ExampleProperty { get; set; }

    public Guid? UUID { get; set; }

    public string Key { get; set; } = "Primary";

    public override string PartitionKey => Key;

    public TestAuditableObject()
    {
    }
}