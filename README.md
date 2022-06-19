# Overview
This packages implements the Generic Repository pattern for interacting with data in CosmosDB. The goal is to leverage CosmosDB's schemaless capabilities to allow developers 
to interact only with Plain Old C# Objects (POCOs) and not have to worry about any of the complexity of integrating with CosmosDB. The purpose is to make it easier to deal with
90% of the interaction with CosmosDB. You'll notice that we expose the CosmosDB client out of the repository to allow you to cover the other 10% with your own custom code.

Here is a sample of what using the GenericRepository looks like: 
```csharp
/// A sample POCO that inherits from BaseStorableEntity
var item = new TestObject
{
    Collection = new List<string>
    {
        "TestVal1",
        "TestVal2"
    },
    Number = 5,
    Prop1 = "TestingString",
    ChildObject = new TestObject
    {
        Number = 8,
        Prop1 = "ChildObject"
    }
};

// Inserting the document into CosmosDB
var result = await _testRepo.Upsert(item);

// retrieving a document by id
var retrieved = await _testRepo.Get(result);

// retrieving a document using a predicate (lambda expression)
var doc = await _testRepo.FindOne(x => x.Id == result);

```

The real power of this library is the ability to dynamically query your data using your strongly typed objects.
```csharp

```


# Setting up the library

# CRUD Operations

# Logical vs Physical Delete
