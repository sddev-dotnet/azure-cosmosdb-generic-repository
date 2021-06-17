using SDDev.Net.GenericRepository.Contracts.Search;
using System;
using System.Collections.Generic;

namespace SDDev.Net.GenericRepository.Tests.TestModels
{
    public class TestSearchModel : SearchModel
    {
        public List<string> CollectionValues { get; set; }

        public string StrVal { get; set; }

        public int Num { get; set; }

        public Guid? UUID { get; set; }

        public string ContainsStrTest { get; set; }

        public TestSearchModel()
        {
            CollectionValues = new List<string>();
        }
    }
}