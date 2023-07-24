# Overview
The Contracts Project defines the models and interfaces used throughout the SDDev.Net.GenericRepository library. The reason it is separated from the main library package is in case you want to reference the `IRepository` interface, `IStorableEntity` interface, etc. without including the implementation dependencies on CosmosDB. This allows you, in your code, to separate your contract objects from your implementation while still being able to leverage the GenericRepository with those objects.

Here are a couple of the most important interfaces in this library: 

|Interface | Description | Notes |
|:--------:| ----------- | ------ |
| `IStorableEntity` | All items stored by the GenericRepository must implement the IStorableEntity interface | There is a `BaseStorableEntity` abstract implementation of this class that we recommend using to ensure you're setting keys properly |
| `IAuditableEntity` | Extends the `IStorableEntity` and stores timestamps for when the object was created and last modified as UTC DateTime objects | `BaseAuditableEntity` abstract class can be inherited from to automatically implement this interface |
| `IRepository<T>` | The implementation agnostic interface for the library. Use this to represent the service that interacts with the database. | `GenericRepository<T>` implements this interface 
| `ISearchModel` | Interface to represent a serializable class that can be converted to a predicate using PredicateBuilder | `SearchModel` implements this interface and is not abstract so you can use it directly if you don't have any special filtering requirements.