using SDDev.Net.GenericRepository.Contracts.BaseEntity;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace SDDev.Net.GenericRepository.Contracts.Repository.Patch;
public interface IPatchOperationCollection<TEntity> : IEnumerable<IPatchOperation> where TEntity : IStorableEntity
{
    /// <summary>
    /// Set operation is similar to Add except with the Array data type. If the target path is a valid array index, the existing element at that index is updated.
    /// </summary>
    /// <param name="value">The value that will be set for the specified <see cref="expressions"/>.</param>
    /// <param name="expressions">Selectors for the property that will be set with the specified <see cref="value"/>.</param>
    void Set<TProperty>(Expression<Func<TEntity, TProperty>> expression, TProperty value);

    /// <summary>
    ///     <para>Add performs one of the following, depending on the target path:</para>
    /// 	<para>• If the target path specifies an element that doesn't exist, it's added.</para>
    /// 	<para>• the target path specifies an element that already exists, its value is replaced.</para>
    /// 	<para>• the target path is a valid array index, a new element is inserted into the array at the specified index.This shifts existing elements after the new element.</para>
    /// 	<para>• the index specified is equal to the length of the array, it appends an element to the array.Instead of specifying an index, you can also use the - character.It also results in the element being appended to the array.</para>
    /// 	<para>Note: Specifying an index greater than the array length results in an error.</para>
    /// </summary>
    /// <param name="value">That value that will be added for the specified <see cref="expressions"/>.</param>
    /// <param name="expressions">Selectors for the property that will be added with the specified <see cref="value"/>.</param>
    void Add<TProperty>(Expression<Func<TEntity, TProperty>> expression, TProperty value);

    /// <summary>
    /// This overload is specifically for adding values to collections.
    /// </summary>
    /// <param name="value">That value that will be added for the specified <see cref="expressions"/>.</param>
    /// <param name="expressions">Selectors for the property that will be added with the specified <see cref="value"/>.</param>
    void Add<TProperty>(Expression<Func<TEntity, IEnumerable<TProperty>>> expression, TProperty value);

    /// <summary>
    /// Replace operation is similar to Set except it follows strict replace only semantics. In case the target path specifies an element or an array that doesn't exist, it results in an error.
    /// </summary>
    /// <typeparam name="TProperty">The data type of the property that is being set.</typeparam>
    /// <param name="value">This is the new value that will replace the existing value.</param>
    /// <param name="expressions">The property to replace with the specified <see cref="value"/>.</param>
    void Replace<TProperty>(Expression<Func<TEntity, TProperty>> expression, TProperty value);

    /// <summary>
    ///     <para>Remove performs one of the following, depending on the target path:</para>
    ///     <para>• If the target path specifies an element that doesn't exist, it results in an error.</para>
    ///     <para>• If the target path specifies an element that already exists, it's removed.</para>
    ///     <para>• If the target path is an array index, it's deleted and any elements above the specified index are shifted back one position.</para>
    ///     <para>Note: Specifying an index equal to or greater than the array length would result in an error.</para>
    /// </summary>
    /// <typeparam name="TProperty"></typeparam>
    /// <param name="expressions"></param>
    void Remove<TProperty>(Expression<Func<TEntity, TProperty>> expression);
}
