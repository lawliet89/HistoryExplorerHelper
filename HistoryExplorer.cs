using System;
using System.Collections.Generic;
using System.Data.Entity.Core.Objects.DataClasses;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using FrameLog.Contexts;
using FrameLog.History;
using FrameLog.Models;
using FrameLog.Translation;

namespace HistoryExplorerHelper
{
    /// <summary>
    /// We wrap the HistoryExplorer that comes with FrameLog so that the result can 
    /// conform to our legacy interfaces. Nothing clever here.
    /// </summary>
    public class HistoryExplorer<TChangeSet, TStaff> where TChangeSet : IChangeSet<TStaff>
    {
        private FrameLog.History.HistoryExplorer<TChangeSet, TStaff> inner;

        public HistoryExplorer(IHistoryContext<TChangeSet, TStaff> context)
        {
            inner = new FrameLog.History.HistoryExplorer<TChangeSet, TStaff>(context, new LegacyBindManager(context));
            rehydrateCollectionMethod = GetType().GetMethod("rehydrateCollection", BindingFlags.Instance | BindingFlags.NonPublic);
            getObjectAtMethod = GetType().GetMethods().Single(m => m.HasAttribute<GetObjectAtAttribute>());
            rehydrateChildrenMethod = GetType().GetMethod("rehydrateChildren", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        public IEnumerable<IChange<TValue, TStaff>> ChangesTo<TModel, TValue>(TModel model, 
            Expression<Func<TModel, TValue>> property)
        {
            return inner.ChangesTo(model, property);
        }

        public IEnumerable<IChange<TModel, TStaff>> ChangesTo<TModel>(TModel model)
            where TModel : ICloneable, new()
        {
            return inner.ChangesTo(model);
        }
        public IEnumerable<IChange<TModel, TStaff>> ChangesTo<TModel>(string reference)
            where TModel : ICloneable, new()
        {
            return inner.ChangesTo<TModel>(reference);
        }

        public virtual IChange<TModel, TStaff> GetCreation<TModel>(TModel model)
        {
            return inner.GetCreation(model);
        }

        /// <summary>
        /// Returns a snapshot of the object as it was at the given time.
        /// 
        /// Be careful: Not all objects in Daylight have complete histories, as FrameLog
        /// was added after Daylight had been operation for a while. Using this method
        /// on those objects may result in incorrect results.
        /// 
        /// Warning: Recursive Hydration is a destructive operation on your ObjectContext.
        /// Relationships can be destroyed during the operation.
        /// Refreshing objects will NOT refresh removed relationships.
        /// It'll be best to perform this in a separate ObjectContext.
        /// 
        /// See <see cref="GetObjectAtAttribute"/> for an explanation on the attribute
        /// </summary>
        [GetObjectAt]
        public TModel GetObjectAt<TModel>(TModel model, DateTime dateTime,
            Func<IEnumerable<IChange<TModel, TStaff>>, IChange<TModel, TStaff>> versionSelector = null, 
            bool recursiveHydration = false)
            where TModel : ICloneable, new()
        {
            if (model == null)
                return default(TModel);
            var changes = ChangesTo(model);
            return getObjectAt(model, changes, dateTime, versionSelector, recursiveHydration);
        }

        public TModel GetObjectAt<TModel>(string reference, DateTime dateTime,
            Func<IEnumerable<IChange<TModel, TStaff>>, IChange<TModel, TStaff>> versionSelector = null,
            bool recursiveHydration = false)
            where TModel : ICloneable, new()
        {
            if (string.IsNullOrEmpty(reference))
                return default(TModel);
            var changes = ChangesTo<TModel>(reference);
            return getObjectAt(default(TModel), changes, dateTime, versionSelector, recursiveHydration);
        }

        private TModel getObjectAt<TModel>(TModel model, IEnumerable<IChange<TModel, TStaff>> changes, DateTime dateTime,
            Func<IEnumerable<IChange<TModel, TStaff>>, IChange<TModel, TStaff>> versionSelector = null, bool recursiveHydration = false)
            where TModel : ICloneable, new()
        {
            var obj = model;
            changes = changes.Where(c => c.Timestamp <= dateTime)
                .OrderByDescending(c => c.Timestamp);
            var change = versionSelector == null ? changes.FirstOrDefault() : versionSelector(changes);

            if (change != null)
            {
                obj = change.Value;
            }
            if (recursiveHydration)
            {
                obj = rehydrateChildren(obj, dateTime, new HashSet<object>());
            }
            return obj;
        }

        // Recursively rehydrate children navigation property from FrameLog
        // because FrameLog does not rehydrate them by default.
        private readonly MethodInfo rehydrateChildrenMethod;
        private readonly MethodInfo getObjectAtMethod;
        private readonly MethodInfo rehydrateCollectionMethod;
        private TModel rehydrateChildren<TModel>(TModel model, DateTime dateTime, ISet<object> objectSet)
            where TModel : ICloneable, new()
        {
            if (model == null)
                return default(TModel);
            if (!objectSet.Add(model))
                return model;

            foreach (var property in model.GetNavigationProperties())
            {
                if (property.IsEntityCollection())
                {
                    var entityType = property.PropertyType.GetGenericArguments()[0];
                    if (!entityType.ImplementsInterface<ICloneable>() || !entityType.HasEmptyConstructor())
                        continue;
                    var collection = property.GetValue(model, BindingFlags.Public | BindingFlags.NonPublic, null, null,
                        null);
                    if (collection == null)
                        continue;
                    var method = rehydrateCollectionMethod.MakeGenericMethod(entityType);
                    method.Invoke(this, new[] {collection, dateTime, objectSet});
                }
                else if (property.PropertyType.ImplementsInterface<ICloneable>()
                         && property.PropertyType.HasEmptyConstructor())
                {
                    var value = property.GetValue(model, BindingFlags.Public | BindingFlags.NonPublic, null, null,
                        null);
                    if (value == null)
                        continue;

                    var objectAtGenericMethod = getObjectAtMethod.MakeGenericMethod(property.PropertyType);
                    var objectAt = objectAtGenericMethod.Invoke(this, new[] { value, dateTime, null, false });
                    if (objectAt == null)
                        continue;
                    value = objectAt;

                    var method = rehydrateChildrenMethod.MakeGenericMethod(property.PropertyType);
                    var childObject = method.Invoke(this, new[]
                    {
                        value, dateTime, objectSet
                    });
                    if (childObject == null)
                        continue;
                    property.SetValue(model, childObject, BindingFlags.Public | BindingFlags.NonPublic, null, null,
                        null);
                }
            }

            return model;
        }

        // ReSharper disable once UnusedMember.Local -- Used in reflection
        private void rehydrateCollection<TModel>(EntityCollection<TModel> collection, DateTime dateTime,
            ISet<object> objectSet)
            where TModel : class, ICloneable, new()
        {
            var items = collection.ToList();
            collection.Clear();
            foreach (var item in items)
            {
                if (!objectSet.Add(item))
                    continue;
                var newItem = GetObjectAt(item, dateTime);
                newItem = rehydrateChildren(newItem, dateTime, objectSet);
                collection.Add(item);
            }
            
        }     
    }

    /// <summary>
    /// There are two overloads of GetObjectAt and it's a pain to use reflection to get
    /// the correct overloaded method. This attribute is a way to "cheat" the process
    /// by adding the attribute to the method we want and then retrieving it
    /// by GetMethods().Single(m => m.HasAttribute)
    /// <see cref="getObjectAtMethod"/>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    internal class GetObjectAtAttribute : Attribute { }
}