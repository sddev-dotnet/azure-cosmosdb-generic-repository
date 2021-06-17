using SDDev.Net.GenericRepository.Contracts.BaseEntity;

namespace SDDev.Net.GenericRepository.Tests.TestModels
{
    public class AnotherTestObject : BaseStorableEntity
    {
        public string Name { get; set; }

        public int Quantity { get; set; }

        public bool IsDeleted { get; set; }


    }
}
