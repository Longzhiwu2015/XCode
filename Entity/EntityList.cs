﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Text;
using System.Xml.Serialization;
using NewLife.IO;
using NewLife.Reflection;
using XCode.Common;
using XCode.Configuration;

#if NET4
using System.Linq;
#else

#endif

namespace XCode
{
    /// <summary>实体集合，提供批量查询和批量操作实体等操作。若需要使用Linq，需要先用<see cref="ToList"/>方法。</summary>
    /// <remarks>
    /// 在.Net 2.0时代，没有Linq可用时，该类的对象查询等方法发挥了很大的作用。
    /// 但是弱类型比较的写法，不太方便，并且有时会带来非常难以查找的错误。
    /// 比如Object.Equal比较Int16和Int32两个数字，是不相等的，也就是说，如果有个Int32字段，传入Int16的数字是无法找到任何匹配项的。
    /// 
    /// 后来在.Net 2.0上实现了Linq，该类的对象查询方法将会逐步淡出，建议优先考虑Linq。
    /// </remarks>
    [Serializable]
    public partial class EntityList<T> : List<T>, IEntityList, IList, IList<IEntity>, IListSource, IEnumerable where T : IEntity
    {
        #region 构造函数
        /// <summary>构造一个实体对象集合</summary>
        public EntityList() { }

        /// <summary>构造一个实体对象集合</summary>
        /// <param name="collection"></param>
        public EntityList(IEnumerable<T> collection) : base(collection) { }

        /// <summary>构造一个实体对象集合</summary>
        /// <param name="capacity"></param>
        public EntityList(Int32 capacity) : base(capacity) { }

        /// <summary>初始化</summary>
        /// <param name="collection"></param>
        public EntityList(IEnumerable collection)
        {
            if (collection != null)
            {
                foreach (T item in collection)
                {
                    Add(item);
                }
            }
        }

        /// <summary>已重载。</summary>
        /// <returns></returns>
        public override string ToString()
        {
            return String.Format("EntityList<{0}>[Count={1}]", typeof(T).Name, Count);
        }
        #endregion

        #region 重载运算符
        /// <summary>集合相加</summary>
        /// <param name="entities1">第一个实体集合</param>
        /// <param name="entities2">第二个实体集合</param>
        /// <returns></returns>
        public static EntityList<T> operator +(EntityList<T> entities1, EntityList<T> entities2)
        {
            if ((entities1 == null || entities1.Count < 1) && (entities2 == null || entities2.Count < 1)) return entities1;

            var list = new EntityList<T>();
            if (entities1 != null && entities1.Count > 0) list.AddRange(entities1);
            if (entities2 != null && entities2.Count > 0) list.AddRange(entities2);

            return list;
        }

        /// <summary>集合相减</summary>
        /// <param name="entities1">第一个实体集合</param>
        /// <param name="entities2">第二个实体集合</param>
        /// <returns></returns>
        public static EntityList<T> operator -(EntityList<T> entities1, EntityList<T> entities2)
        {
            if (entities1 == null || entities1.Count < 1) return entities1;

            var list = new EntityList<T>();
            foreach (var item in entities1)
            {
                if (entities2 != null && !entities2.Contains(item)) list.Add(item);
            }

            return list;
        }
        #endregion

        #region 集合操作
        /// <summary>从集合中移除另一个集合指定的元素</summary>
        /// <param name="collection"></param>
        public EntityList<T> RemoveRange(IEnumerable<T> collection)
        {
            if (collection == null) return this;

            foreach (T item in collection)
            {
                if (Contains(item)) Remove(item);
            }

            return this;
        }
        #endregion

        #region 对象查询
        /// <summary>根据指定项查找</summary>
        /// <param name="name">属性名</param>
        /// <param name="value">属性值</param>
        /// <returns></returns>
        public EntityList<T> FindAll(String name, Object value)
        {
            if (Count < 1) return this;

            // 先排除掉没有必要的查找，唯一键空值查找没有意义
            FieldItem field = Factory.Table.FindByName(name);
            if (field != null && (field.IsIdentity || field.PrimaryKey))
            {
                // 唯一键为空时，比如自增且参数小于等于0时，返回空
                if (Helper.IsNullKey(value)) return null;
            }

            var list = new EntityList<T>();

            // 特殊处理整数类型，避免出现相同值不同整型而导致结果不同
            if (value != null && value.GetType().IsIntType())
            {
                // 整型统一转为Int64后再比较，因为即使数值相等，类型不同的对象也是不等的
                var v6 = Convert.ToInt64(value);
                foreach (var item in this)
                {
                    if (item == null) continue;
                    if (Convert.ToInt64(item[name]) == v6) list.Add(item);
                }
            }
            else
            {
                foreach (var item in this)
                {
                    if (item == null) continue;
                    if (Object.Equals(item[name], value)) list.Add(item);
                }
            }
            return list;
        }

        /// <summary>根据指定项查找</summary>
        /// <param name="names">属性名集合</param>
        /// <param name="values">属性值集合</param>
        /// <returns></returns>
        public EntityList<T> FindAll(String[] names, Object[] values)
        {
            if (Count < 1) return this;

            FieldItem field = Factory.Table.FindByName(names[0]);
            if (field != null && (field.IsIdentity || field.PrimaryKey))
            {
                // 唯一键为自增且参数小于等于0时，返回空
                if (Helper.IsNullKey(values[0])) return null;
            }

            // 特殊处理整数类型，避免出现相同值不同整型而导致结果不同
            var ts = new Boolean[values.Length];
            var vs = new Int64[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == null) continue;

                // 整型统一转为Int64后再比较，因为即使数值相等，类型不同的对象也是不等的
                ts[i] = values[i].GetType().IsIntType();
                if (ts[i]) vs[i] = Convert.ToInt64(values[i]);
            }

            var list = new EntityList<T>();
            foreach (var item in this)
            {
                if (item == null) continue;

                var b = true;
                for (int i = 0; i < names.Length; i++)
                {
                    var iv = item[names[i]];
                    if (!Object.Equals(iv, values[i]) && !(ts[i] && Convert.ToInt64(iv) == vs[i]))
                    {
                        b = false;
                        break;
                    }
                }
                if (b) list.Add(item);
            }
            return list;
        }

        /// <summary>检索与指定谓词定义的条件匹配的所有元素。</summary>
        /// <param name="match">条件</param>
        /// <returns></returns>
        public new EntityList<T> FindAll(Predicate<T> match)
        {
            if (Count < 1) return this;

            var list = new EntityList<T>();
            foreach (var item in this)
            {
                if (item == null) continue;
                if (match(item)) list.Add(item);
            }
            return list;
        }

        /// <summary>根据指定项查找</summary>
        /// <param name="name">属性名</param>
        /// <param name="value">属性值</param>
        /// <returns></returns>
        public T Find(String name, Object value)
        {
            if (Count < 1) return default(T);

            // 特殊处理整数类型，避免出现相同值不同整型而导致结果不同
            if (value != null && value.GetType().IsIntType())
            {
                // 整型统一转为Int64后再比较，因为即使数值相等，类型不同的对象也是不等的
                var v6 = Convert.ToInt64(value);
                foreach (var item in this)
                {
                    if (item == null) continue;
                    if (Convert.ToInt64(item[name]) == v6) return item;
                }
                return default(T);
            }
            else
            {
                foreach (var item in this)
                {
                    if (item == null) continue;
                    if (Object.Equals(item[name], value)) return item;
                }
                return default(T);
            }
        }

        /// <summary>根据指定项查找字符串。忽略大小写</summary>
        /// <param name="name">属性名</param>
        /// <param name="value">属性值</param>
        /// <returns></returns>
        public EntityList<T> FindAllIgnoreCase(String name, String value)
        {
            if (Count < 1) return this;

            var list = new EntityList<T>();
            foreach (var item in this)
            {
                if (item == null) continue;
                if (String.Equals((String)item[name], value, StringComparison.OrdinalIgnoreCase)) list.Add(item);
            }
            return list;
        }

        /// <summary>根据指定项查找字符串。忽略大小写</summary>
        /// <param name="name">属性名</param>
        /// <param name="value">属性值</param>
        /// <returns></returns>
        public T FindIgnoreCase(String name, String value)
        {
            if (Count < 1) return default(T);

            foreach (var item in this)
            {
                if (item == null) continue;
                if (String.Equals((String)item[name], value, StringComparison.OrdinalIgnoreCase)) return item;
            }
            return default(T);
        }

        /// <summary>集合是否包含指定项</summary>
        /// <param name="name">名称</param>
        /// <param name="value">数值</param>
        /// <returns></returns>
        public Boolean Exists(String name, Object value)
        {
            return Find(name, value) != null;
            //if (Count < 1) return false;

            //foreach (T item in this)
            //{
            //    if (item == null) continue;
            //    if (Object.Equals(item[name], value)) return true;
            //}
            //return false;
        }
        #endregion

        #region IEntityList接口
        /// <summary>根据指定项查找</summary>
        /// <param name="name">属性名</param>
        /// <param name="value">属性值</param>
        /// <returns></returns>
        IEntityList IEntityList.FindAll(String name, Object value) { return FindAll(name, value); }

        /// <summary>根据指定项查找</summary>
        /// <param name="names">属性名</param>
        /// <param name="values">属性值</param>
        /// <returns></returns>
        IEntityList IEntityList.FindAll(String[] names, Object[] values) { return FindAll(names, values); }

        /// <summary>根据指定项查找</summary>
        /// <param name="name">属性名</param>
        /// <param name="value">属性值</param>
        /// <returns></returns>
        IEntity IEntityList.Find(String name, Object value) { return Find(name, value); }

        /// <summary>根据指定项查找字符串。忽略大小写</summary>
        /// <param name="name">属性名</param>
        /// <param name="value">属性值</param>
        /// <returns></returns>
        IEntityList IEntityList.FindAllIgnoreCase(String name, String value) { return FindAllIgnoreCase(name, value); }

        /// <summary>根据指定项查找字符串。忽略大小写</summary>
        /// <param name="name">属性名</param>
        /// <param name="value">属性值</param>
        /// <returns></returns>
        IEntity IEntityList.FindIgnoreCase(String name, String value) { return FindIgnoreCase(name, value); }

        /// <summary>设置所有实体中指定项的值</summary>
        /// <param name="name">指定项的名称</param>
        /// <param name="value">数值</param>
        IEntityList IEntityList.SetItem(String name, Object value) { return SetItem(name, value); }

        IEntityList IEntityList.FromXml(String xml) { return FromXml(xml); }
        #endregion

        #region 对象操作
        /// <summary>把整个集合插入到数据库</summary>
        /// <param name="useTransition">是否使用事务保护</param>
        /// <returns></returns>
        public Int32 Insert(Boolean useTransition)
        {
            if (Count < 1) return 0;

            var count = 0;

            if (useTransition)
            {
                var dal = Factory;
                dal.BeginTransaction();
                try
                {
                    foreach (T item in this)
                    {
                        count += item.Insert();
                    }

                    dal.Commit();
                }
                catch
                {
                    dal.Rollback();
                    throw;
                }
            }
            else
            {
                foreach (T item in this)
                {
                    count += item.Insert();
                }
            }

            return count;
        }

        /// <summary>把整个集合插入到数据库</summary>
        /// <returns></returns>
        public Int32 Insert() { return Insert(true); }

        /// <summary>把整个集合更新到数据库</summary>
        /// <param name="useTransition">是否使用事务保护</param>
        /// <returns></returns>
        public Int32 Update(Boolean useTransition)
        {
            if (Count < 1) return 0;

            var count = 0;

            if (useTransition)
            {
                var dal = Factory;
                dal.BeginTransaction();
                try
                {
                    foreach (T item in this)
                    {
                        count += item.Update();
                    }

                    dal.Commit();
                }
                catch
                {
                    dal.Rollback();
                    throw;
                }
            }
            else
            {
                foreach (T item in this)
                {
                    count += item.Update();
                }
            }

            return count;
        }

        /// <summary>把整个集合更新到数据库</summary>
        /// <returns></returns>
        public Int32 Update() { return Update(true); }

        /// <summary>把整个保存更新到数据库</summary>
        /// <param name="useTransition">是否使用事务保护</param>
        /// <returns></returns>
        public Int32 Save(Boolean useTransition)
        {
            if (Count < 1) return 0;

            var count = 0;

            if (useTransition)
            {
                var dal = Factory;
                dal.BeginTransaction();
                try
                {
                    foreach (T item in this)
                    {
                        count += item.Save();
                    }

                    dal.Commit();
                }
                catch
                {
                    dal.Rollback();
                    throw;
                }
            }
            else
            {
                foreach (T item in this)
                {
                    count += item.Save();
                }
            }

            return count;
        }

        /// <summary>把整个集合保存到数据库</summary>
        /// <returns></returns>
        public Int32 Save() { return Save(true); }

        /// <summary>把整个集合从数据库中删除</summary>
        /// <param name="useTransition">是否使用事务保护</param>
        /// <returns></returns>
        public Int32 Delete(Boolean useTransition)
        {
            if (Count < 1) return 0;

            var count = 0;

            if (useTransition)
            {
                var dal = Factory;
                dal.BeginTransaction();
                try
                {
                    foreach (T item in this)
                    {
                        count += item.Delete();
                    }

                    dal.Commit();
                }
                catch
                {
                    dal.Rollback();
                    throw;
                }
            }
            else
            {
                foreach (T item in this)
                {
                    count += item.Delete();
                }
            }

            return count;
        }

        /// <summary>把整个集合从数据库中删除</summary>
        /// <returns></returns>
        public Int32 Delete() { return Delete(true); }

        /// <summary>设置所有实体中指定项的值</summary>
        /// <param name="name">指定项的名称</param>
        /// <param name="value">数值</param>
        public EntityList<T> SetItem(String name, Object value)
        {
            if (Count < 1) return this;

            foreach (var item in this)
            {
                if (item == null) continue;
                if (!Object.Equals(item[name], value)) item.SetItem(name, value);
            }

            return this;
        }

        /// <summary>获取所有实体中指定项的值，不允许重复项</summary>
        /// <typeparam name="TResult">指定项的类型</typeparam>
        /// <param name="name">指定项的名称</param>
        /// <returns></returns>
        public List<TResult> GetItem<TResult>(String name) { return GetItem<TResult>(name, false); }

        /// <summary>获取所有实体中指定项的值</summary>
        /// <remarks>
        /// 有时候只是为了获取某个属性值的集合，可以允许重复项，而有时候是获取唯一主键，作为in操作的参数，不该允许重复项。
        /// </remarks>
        /// <typeparam name="TResult">指定项的类型</typeparam>
        /// <param name="name">指定项的名称</param>
        /// <param name="allowRepeated">是否允许重复项</param>
        /// <returns></returns>
        public List<TResult> GetItem<TResult>(String name, Boolean allowRepeated)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException("name");

            var list = new List<TResult>();
            if (Count < 1) return list;

            var type = typeof(TResult);
            foreach (var item in this)
            {
                if (item == null) continue;

                // 避免集合插入了重复项
                var obj = TypeX.ChangeType<TResult>(item[name]);
                if (!allowRepeated && !list.Contains(obj)) list.Add(obj);
            }
            return list;
        }

        /// <summary>串联指定成员，方便由实体集合构造用于查询的子字符串</summary>
        /// <param name="name">名称</param>
        /// <param name="separator"></param>
        /// <returns></returns>
        public String Join(String name, String separator)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException("name");

            if (Count < 1) return null;

            var list = GetItem<String>(name);
            if (list == null || list.Count < 1) return null;

            return String.Join(separator, list.ToArray());
        }

        /// <summary>串联</summary>
        /// <param name="separator"></param>
        /// <returns></returns>
        public String Join(String separator)
        {
            if (Count < 1) return null;

            var sb = new StringBuilder(Count * 10);
            foreach (var item in this)
            {
                if (sb.Length > 0) sb.Append(separator);
                sb.Append("" + item);
            }
            return sb.ToString();
        }
        #endregion

        #region 排序
        /// <summary>按指定字段排序</summary>
        /// <param name="name">字段</param>
        /// <param name="isDesc">是否降序</param>
        public EntityList<T> Sort(String name, Boolean isDesc)
        {
            if (Count < 1) return this;

            var type = GetItemType(name);
            if (!typeof(IComparable).IsAssignableFrom(type)) throw new NotSupportedException("不支持比较！");

            var n = 1;
            if (isDesc) n = -1;

            Sort((item1, item2) =>
            {
                // Object.Equals可以有效的处理两个元素都为空的问题
                if (Object.Equals(item1[name], item2[name])) return 0;
                return (item1[name] as IComparable).CompareTo(item2[name]) * n;
            });

            return this;
        }

        /// <summary>按指定字段数组排序</summary>
        /// <param name="names">字段</param>
        /// <param name="isDescs">是否降序</param>
        public EntityList<T> Sort(String[] names, Boolean[] isDescs)
        {
            if (Count < 1) return this;

            for (int i = 0; i < names.Length; i++)
            {
                var name = names[i];
                var isDesc = isDescs[i];

                var type = GetItemType(name);
                if (!typeof(IComparable).IsAssignableFrom(type)) throw new NotSupportedException("不支持比较！");
            }

            Sort((item1, item2) =>
            {
                // 逐层对比
                for (int i = 0; i < names.Length; i++)
                {
                    String name = names[i];
                    Boolean isDesc = isDescs[i];

                    Int32 n = 1;
                    if (isDesc) n = -1;

                    // Object.Equals可以有效的处理两个元素都为空的问题
                    if (Object.Equals(item1[name], item2[name]))
                        n = 0;
                    else
                        n = (item1[name] as IComparable).CompareTo(item2[name]) * n;
                    if (n != 0) return n;
                }
                return 0;
            });

            return this;
        }

        Type GetItemType(String name)
        {
            if (String.IsNullOrEmpty(name) || EntityType == null) return null;

            PropertyInfoX pi = PropertyInfoX.Create(EntityType, name);
            if (pi != null) return pi.Type;

            FieldInfoX fi = FieldInfoX.Create(EntityType, name);
            if (fi != null) return fi.Type;

            return null;
        }

        /// <summary>按指定字段排序</summary>
        /// <param name="name">字段</param>
        /// <param name="isDesc">是否降序</param>
        IEntityList IEntityList.Sort(String name, Boolean isDesc) { return Sort(name, isDesc); }

        /// <summary>按指定字段数组排序</summary>
        /// <param name="names">字段</param>
        /// <param name="isDescs">是否降序</param>
        IEntityList IEntityList.Sort(String[] names, Boolean[] isDescs) { return Sort(names, isDescs); }

        /// <summary>提升指定实体在当前列表中的位置，加大排序键的值</summary>
        /// <param name="entity"></param>
        /// <param name="sortKey"></param>
        /// <returns></returns>
        public EntityList<T> Up(T entity, String sortKey)
        {
            if (Count < 1) return this;
            if (entity == null) throw new ArgumentNullException("entity");

            if (String.IsNullOrEmpty(sortKey) && Factory.FieldNames.Contains("Sort")) sortKey = "Sort";
            if (String.IsNullOrEmpty(sortKey)) throw new ArgumentNullException("sortKey");

            // 要先排序
            Sort(sortKey, true);

            for (int i = 0; i < Count; i++)
            {
                Int32 s = Count - i;
                // 当前项，排序增加。原来比较实体相等有问题，也许新旧实体类不对应，现在改为比较主键值
                if (i > 0 && entity.EqualTo(this[i])) s++;
                // 下一项是当前项，排序减少
                if (i < Count - 1 && entity.EqualTo(this[i + 1])) s--;
                if (s > Count) s = Count;
                this[i].SetItem(sortKey, s);
            }
            Save(true);

            return this;
        }

        /// <summary>降低指定实体在当前列表中的位置，减少排序键的值</summary>
        /// <param name="entity"></param>
        /// <param name="sortKey"></param>
        /// <returns></returns>
        public EntityList<T> Down(T entity, String sortKey)
        {
            if (Count < 1) return this;
            if (entity == null) throw new ArgumentNullException("entity");

            if (String.IsNullOrEmpty(sortKey) && Factory.FieldNames.Contains("Sort")) sortKey = "Sort";
            if (String.IsNullOrEmpty(sortKey)) throw new ArgumentNullException("sortKey");

            // 要先排序
            Sort(sortKey, true);

            for (int i = 0; i < Count; i++)
            {
                Int32 s = Count - i;
                // 当前项，排序减少
                if (entity.EqualTo(this[i])) s--;
                // 上一项是当前项，排序增加
                if (i >= 1 && entity.EqualTo(this[i - 1])) s++;
                if (s < 1) s = 1;
                this[i].SetItem(sortKey, s);
            }
            Save(true);

            return this;
        }

        /// <summary>提升指定实体在当前列表中的位置，加大排序键的值</summary>
        /// <param name="entity"></param>
        /// <param name="sortKey"></param>
        /// <returns></returns>
        IEntityList IEntityList.Up(IEntity entity, String sortKey) { return Up((T)entity, sortKey); }

        /// <summary>降低指定实体在当前列表中的位置，减少排序键的值</summary>
        /// <param name="entity"></param>
        /// <param name="sortKey"></param>
        /// <returns></returns>
        IEntityList IEntityList.Down(IEntity entity, String sortKey) { return Down((T)entity, sortKey); }
        #endregion

        #region 导入导出
        /// <summary>导出</summary>
        /// <param name="writer"></param>
        public virtual void Export(TextWriter writer)
        {
            IList list = this;
            if (typeof(T).IsInterface) list = (this as IListSource).GetList();

            var serial = CreateXmlSerializer(list.GetType());
            serial.Serialize(writer, list);
        }

        /// <summary>导入</summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        public virtual void Import(TextReader reader)
        {
            var serial = CreateXmlSerializer(this.GetType());
            var list = serial.Deserialize(reader) as EntityList<T>;

            AddRange(list);
        }

        private XmlSerializer CreateXmlSerializer(Type type)
        {
            var ovs = new XmlAttributeOverrides();
            var factory = Factory;
            var entity = factory.Create();
            foreach (var item in factory.Fields)
            {
                var atts = new XmlAttributes();
                atts.XmlAttribute = new XmlAttributeAttribute();
                atts.XmlDefaultValue = entity[item.Name];
                ovs.Add(item.DeclaringType, item.Name, atts);
            }
            return new XmlSerializer(type, ovs);
        }

        /// <summary>导出Xml文本</summary>
        /// <returns></returns>
        public virtual String ToXml()
        {
            using (var stream = new MemoryStream())
            {
                var writer = new StreamWriter(stream, Encoding.UTF8);
                Export(writer);
                var bts = stream.ToArray();
                var xml = Encoding.UTF8.GetString(bts);
                writer.Close();
                if (!String.IsNullOrEmpty(xml)) xml = xml.Trim();
                return xml;
            }
        }

        /// <summary>导入Xml文本</summary>
        /// <param name="xml"></param>
        public virtual EntityList<T> FromXml(String xml)
        {
            if (String.IsNullOrEmpty(xml)) return this;
            xml = xml.Trim();
            using (var reader = new StringReader(xml))
            {
                Import(reader);
            }

            return this;
        }

        /// <summary>导出Json</summary>
        /// <returns></returns>
        public virtual String ToJson()
        {
            return new Json().Serialize(this);
        }

        /// <summary>导入Json</summary>
        /// <param name="json"></param>
        /// <returns></returns>
        public static EntityList<T> FromJson(String json)
        {
            return new Json().Deserialize<EntityList<T>>(json);
        }
        #endregion

        #region 导出DataSet数据集
        /// <summary>转为DataTable</summary>
        /// <param name="allowUpdate">是否允许更新数据，如果允许，将可以对DataTable进行添删改等操作</param>
        /// <returns></returns>
        public DataTable ToDataTable(Boolean allowUpdate = true)
        {
            DataTable dt = new DataTable();
            foreach (FieldItem item in Factory.Fields)
            {
                DataColumn dc = new DataColumn();
                dc.ColumnName = item.Name;
                dc.DataType = item.Type;
                dc.Caption = item.Description;
                dc.AutoIncrement = item.IsIdentity;

                // 关闭这两项，让DataTable宽松一点
                //dc.Unique = item.PrimaryKey;
                //dc.AllowDBNull = item.IsNullable;

                //if (!item.DataObjectField.IsIdentity) dc.DefaultValue = item.Column.DefaultValue;
                dt.Columns.Add(dc);
            }
            // 判断是否有数据，即使没有数据，也需要创建一个空格DataTable
            if (Count > 0)
            {
                foreach (IEntity entity in this)
                {
                    DataRow dr = dt.NewRow();
                    foreach (FieldItem item in Factory.Fields)
                    {
                        dr[item.Name] = entity[item.Name];
                    }
                    dt.Rows.Add(dr);
                }
            }

            // 如果允许更新数据，那么绑定三个事件，委托到实体类的更新操作
            if (allowUpdate)
            {
                dt.RowChanging += new DataRowChangeEventHandler(dt_RowChanging);
                dt.RowDeleting += new DataRowChangeEventHandler(dt_RowDeleting);
                dt.TableNewRow += new DataTableNewRowEventHandler(dt_TableNewRow);
            }

            return dt;
        }

        void dt_TableNewRow(object sender, DataTableNewRowEventArgs e)
        {
            IEntity entity = Factory.FindByKeyForEdit(null);
            DataRow dr = e.Row;
            foreach (FieldItem item in Factory.Fields)
            {
                dr[item.Name] = entity[item.Name];
            }
        }

        void dt_RowChanging(object sender, DataRowChangeEventArgs e)
        {
            IEntity entity = Factory.Create();
            DataRow dr = e.Row;
            foreach (FieldItem item in Factory.Fields)
            {
                //entity[item.Name] = dr[item.Name];
                // 影响脏数据
                entity.SetItem(item.Name, dr[item.Name]);
            }

            if (e.Action == DataRowAction.Add)
                entity.Insert();
            else if (e.Action == DataRowAction.Change)
                entity.Update();
            else
            {
                // 不支持
            }
        }

        void dt_RowDeleting(object sender, DataRowChangeEventArgs e)
        {
            IEntity entity = Factory.Create();
            DataRow dr = e.Row;
            foreach (FieldItem item in Factory.Fields)
            {
                entity[item.Name] = dr[item.Name];
            }

            entity.Delete();
        }

        /// <summary>转为DataSet</summary>
        /// <returns></returns>
        public DataSet ToDataSet()
        {
            DataSet ds = new DataSet();
            ds.Tables.Add(ToDataTable());
            return ds;
        }
        #endregion

        #region 转换
        /// <summary>转为泛型List，方便进行Linq</summary>
        /// <returns></returns>
        public List<T> ToList() { return this; }

        /// <summary>任意集合转为实体集合</summary>
        /// <param name="collection"></param>
        /// <returns></returns>
        public static EntityList<T> From(IEnumerable collection)
        {
            //if (collection == null || collection.GetEnumerator() == null) return null;

            return new EntityList<T>(collection);
        }

        /// <summary>拥有指定类型转换器的转换</summary>
        /// <typeparam name="T2"></typeparam>
        /// <param name="collection"></param>
        /// <param name="func"></param>
        /// <returns></returns>
        public static EntityList<T> From<T2>(IEnumerable collection, Func<T2, T> func)
        {
            //if (collection == null || collection.GetEnumerator() == null) return null;

            EntityList<T> list = new EntityList<T>();
            if (collection == null) return list;
            foreach (T2 item in collection)
            {
                if (item == null) continue;
                T entity = default(T);
                if (func == null)
                    entity = (T)(Object)item;
                else
                    entity = func(item);
                if (entity != null) list.Add(entity);
            }
            //if (list == null || list.Count < 1) return null;
            return list;
        }
        #endregion

        #region IListSource接口
        bool IListSource.ContainsListCollection
        {
            get { return Count > 0; }
        }

        IList IListSource.GetList()
        {
            // 如果是接口，创建新的集合，否则返回自身
            if (!typeof(T).IsInterface) return this;

            //if (Count < 1) return null;

            return ToArray(null);
        }
        #endregion

        #region 复制
        IList ToArray(Type type)
        {
            //if (Count < 1) return null;

            // 元素类型
            if (type == null) type = EntityType;
            // 泛型
            type = typeof(EntityListView<>).MakeGenericType(type);

            // 初始化集合，实际上是创建了一个真正的实体类型
            IList list = type.CreateInstance() as IList;
            for (int i = 0; i < Count; i++)
            {
                list.Add(this[i]);
            }

            return list;
        }
        #endregion

        #region 辅助函数
        private static EntityList<T> _Empty;
        /// <summary>空集合</summary>
        [Obsolete("该属性容易让人误操作，比如给空列表加入元素！")]
        public static EntityList<T> Empty { get { return _Empty ?? (_Empty = new EntityList<T>()); } }

        /// <summary>真正的实体类型。有些场合为了需要会使用IEntity。</summary>
        Type EntityType
        {
            get
            {
                Type type = typeof(T);
                if (!type.IsInterface) return type;

                if (Count > 0) return this[0].GetType();

                return type;
            }
        }

        /// <summary>实体操作者</summary>
        IEntityOperate Factory
        {
            get
            {
                Type type = EntityType;
                if (type.IsInterface) return null;

                return EntityFactory.CreateOperate(type);
            }
        }
        #endregion

        #region IList<IEntity> 成员
        private static bool IsCompatibleObject(IEntity value)
        {
            if (!(value is T) && value != null || typeof(T).IsValueType) return false;
            return true;
        }

        private static void VerifyValueType(IEntity value)
        {
            if (!IsCompatibleObject(value)) throw new ArgumentException(String.Format("期待{0}类型的参数！", typeof(T).Name), "value");
        }

        int IList<IEntity>.IndexOf(IEntity item)
        {
            if (!IsCompatibleObject(item)) return -1;
            return IndexOf((T)item);
        }

        void IList<IEntity>.Insert(int index, IEntity item)
        {
            VerifyValueType(item);
            Insert(index, (T)item);
        }

        //void IList<IEntity>.RemoveAt(int index)
        //{
        //    RemoveAt(index);
        //}

        IEntity IList<IEntity>.this[int index]
        {
            get { return this[index]; }
            set
            {
                VerifyValueType(value);
                this[index] = (T)value;
            }
        }
        #endregion

        #region ICollection<IEntity> 成员

        void ICollection<IEntity>.Add(IEntity item)
        {
            VerifyValueType(item);
            Add((T)item);
        }

        //void ICollection<IEntity>.Clear()
        //{
        //    throw new NotImplementedException();
        //}

        bool ICollection<IEntity>.Contains(IEntity item)
        {
            if (!IsCompatibleObject(item)) return false;

            return Contains((T)item);
        }

        void ICollection<IEntity>.CopyTo(IEntity[] array, int arrayIndex)
        {
            if (array == null || array.Length == 0) return;

            VerifyValueType(array[0]);
            T[] arr = new T[array.Length];
            CopyTo(arr, arrayIndex);
            for (int i = arrayIndex; i < array.Length; i++)
            {
                array[i] = arr[i];
            }
        }

        //int ICollection<IEntity>.Count
        //{
        //    get { return Count; }
        //}

        bool ICollection<IEntity>.IsReadOnly
        {
            get { return (this as ICollection<T>).IsReadOnly; }
        }

        bool ICollection<IEntity>.Remove(IEntity item)
        {
            if (!IsCompatibleObject(item)) return false;

            return Remove((T)item);
        }

        #endregion

        #region IEnumerable<IEntity> 成员
        IEnumerator<IEntity> IEnumerable<IEntity>.GetEnumerator()
        {
            //return new EntityEnumerator(this);

            foreach (T item in this)
            {
                yield return item;
            }
        }

        //class EntityEnumerator : IEnumerator<IEntity>
        //{
        //    EntityList<T> _list;
        //    Int32 index;
        //    T current;

        //    public EntityEnumerator(EntityList<T> list) { _list = list; }

        //    #region IEnumerator<IEntity> 成员

        //    public IEntity Current
        //    {
        //        get { return current; }
        //    }

        //    #endregion

        //    #region IDisposable 成员
        //    public void Dispose() { }
        //    #endregion

        //    #region IEnumerator 成员

        //    object IEnumerator.Current
        //    {
        //        get { return current; }
        //    }

        //    public bool MoveNext()
        //    {
        //        if (index >= _list.Count) return false;

        //        current = _list[index++];

        //        return true;
        //    }

        //    public void Reset()
        //    {
        //        index = 0;
        //        current = default(T);
        //    }
        //    #endregion
        //}
        #endregion
    }
}