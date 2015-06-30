using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity.Core.Objects.DataClasses;
using System.Linq;
using System.Reflection;

namespace HistoryExplorerHelper
{
    internal static class ReflectionHelpers
    {
        internal static IEnumerable<T> GetAttributes<T>(this IEnumerable<Type> types)
        {
            List<T> attributes = new List<T>();
            foreach (Type type in types)
                attributes.AddRange(type.GetCustomAttributes(false).Where(a => a is T).Cast<T>());
            return attributes;
        }

        internal static bool HasAttribute<T>(this MemberInfo member)
        {
            return member.GetCustomAttributes(typeof(T), true).Any();
        }
        internal static bool HasAttribute<T>(this MemberInfo member, bool useMetadataType)
        {
            MetadataTypeAttribute attribute = null;
            if (member.DeclaringType != null)
                attribute = member.DeclaringType.GetAttribute<MetadataTypeAttribute>();

            if (attribute != null)
                return member.HasAttribute<T>(new Type[] { attribute.MetadataClassType });
            else
                return member.HasAttribute<T>();
        }
        internal static bool HasAttribute<T>(this MemberInfo member, IEnumerable<Type> metadataTypes)
        {
            if (member.HasAttribute<T>())
                return true;

            foreach (Type type in metadataTypes)
            {
                MemberInfo[] members = type.GetMember(member.Name);
                if (members.Length > 0 && members[0].HasAttribute<T>())
                    return true;
            }
            return false;
        }
        internal static bool HasAttribute<T>(this PropertyDescriptor p)
        {
            return p.Attributes.OfType<T>().Any();
        }

        internal static T GetAttribute<T>(this MemberInfo member)
        {
            return member.GetAttributes<T>().SingleOrDefault();
        }
        internal static IEnumerable<T> GetAttributes<T>(this MemberInfo member)
        {
            return member.GetCustomAttributes(typeof(T), true).Cast<T>();
        }
        internal static T GetAttribute<T>(this PropertyInfo property, IEnumerable<Type> types)
        {
            return property.GetAttributes<T>(types).First();
        }

        internal static IEnumerable<T> GetAttributes<T>(this PropertyInfo property, IEnumerable<Type> types)
        {
            List<T> attributes = new List<T>();
            foreach (Type type in types)
            {
                PropertyInfo metaProperty = type.GetProperty(property.Name);
                if (metaProperty != null)
                    attributes.AddRange(metaProperty.GetCustomAttributes(false).Where(a => a is T).Cast<T>());
            }
            return attributes;
        }

        internal static Type GetGenericInterfaceOfType(this Type type, Type genericInterface)
        {
            return
                type.GetInterfaces()
                    .SingleOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == genericInterface);
        }

        internal static bool HasGenericInterface(this Type type, Type genericInterface)
        {
            return type.GetGenericInterfaceOfType(genericInterface) != null;
        }

        internal static IEnumerable<PropertyInfo> GetNavigationProperties<TModel>(this TModel model)
        {
            return model.GetType().GetNavigationProperties();
        }
        // Navigation properties have the EdmRelationshipNavigationPropertyAttribute attribute
        internal static IEnumerable<PropertyInfo> GetNavigationProperties(this Type type)
        {
            return type.GetProperties()
                .Where(p => p.HasAttribute<EdmRelationshipNavigationPropertyAttribute>());
        }

        internal static bool IsEntityCollection(this PropertyInfo property)
        {
            return property.PropertyType.IsGenericType &&
                   property.PropertyType.GetGenericTypeDefinition() == typeof(EntityCollection<>);
        }

        internal static bool ImplementsInterface<TInterface>(this Type type)
        {
            return type.ImplementsInterface(typeof(TInterface));
        }

        internal static bool ImplementsInterface(this Type type, Type interfaceType)
        {
            return type.GetInterfaces().Contains(interfaceType);
        }

        internal static bool HasEmptyConstructor(this Type type)
        {
            return type.GetConstructor(Type.EmptyTypes) != null;
        }
    }
}
