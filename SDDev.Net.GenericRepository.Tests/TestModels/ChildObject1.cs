using System.Collections.Generic;

namespace SDDev.Net.GenericRepository.Tests.TestModels
{
    public class ChildObject1 : BaseTestObject
    {
        public int SomeIntValue { get; set; }

        public Size ThirtSize { get; set; }

        public override IList<string> ItemType => new List<string>() { "ChildObject1", "BaseTestObject" };

        public override string PartitionKey => "BaseTestObject";
    }
}
