using System.Collections.Generic;

namespace SDDev.Net.GenericRepository.Tests.TestModels
{
    public class ChildObject2 : BaseTestObject
    {
        public string SomeStringProp { get; set; }
        public override IList<string> ItemType => new List<string>() { "ChildObject2", "BaseTestObject" };

        public override string PartitionKey => "BaseTestObject";
    }
}
