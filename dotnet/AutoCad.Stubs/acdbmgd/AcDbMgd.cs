// Stub implementation of Autodesk.AutoCAD.DatabaseServices for CI builds.
// All members throw NotImplementedException at runtime; only compilation is guaranteed.
#pragma warning disable CS8618

using System.Collections;

namespace Autodesk.AutoCAD.DatabaseServices;

public enum OpenMode { ForRead, ForWrite, ForNotify }

public enum DxfCode
{
    LayerName = 8,
}

public struct Handle
{
    public override string ToString() => string.Empty;
}

public class ObjectId
{
    public static ObjectId Null { get; } = new ObjectId();
}

public class ObjectIdCollection : System.Collections.CollectionBase { }

public abstract class DBObject : IDisposable
{
    public virtual void Dispose() { }
}

public abstract class Entity : DBObject
{
    public Handle Handle { get; }
}

public abstract class SymbolTableRecord : DBObject
{
    public string Name { get; set; } = string.Empty;
}

public abstract class SymbolTable : DBObject, IEnumerable
{
    public virtual IEnumerator GetEnumerator() => throw new NotImplementedException();
}

public class Database : DBObject
{
    public ObjectId BlockTableId { get; } = null!;
    public ObjectId DimStyleTableId { get; } = null!;
    public ObjectId LayoutDictionaryId { get; } = null!;
    public ObjectId LayerTableId { get; } = null!;
    public ObjectIdCollection XrefBlockTableRecordIds { get; } = null!;
    public TransactionManager TransactionManager { get; } = null!;
    public string Filename { get; } = string.Empty;
}

public abstract class Transaction : DBObject
{
    public virtual DBObject GetObject(ObjectId id, OpenMode mode) => throw new NotImplementedException();
    public virtual void Commit() { }
    public virtual void Abort() { }
}

public class TransactionManager
{
    public Transaction StartTransaction() => throw new NotImplementedException();
}

public class BlockTable : SymbolTable
{
    public ObjectId this[string name] => throw new NotImplementedException();
    public override IEnumerator GetEnumerator() => throw new NotImplementedException();
}

public class BlockTableRecord : SymbolTableRecord, IEnumerable
{
    public static string ModelSpace { get; } = "*Model_Space";
    public static string PaperSpace { get; } = "*Paper_Space";
    public bool IsAnonymous { get; }
    public bool IsLayout { get; }
    public bool IsDynamicBlock { get; }
    public bool IsFromOverlayReference { get; }
    public bool IsLoaded { get; }
    public string PathName { get; } = string.Empty;
    public IEnumerator GetEnumerator() => throw new NotImplementedException();
}

public class BlockReference : Entity
{
    public ObjectId DynamicBlockTableRecord { get; } = null!;
    public AttributeCollection AttributeCollection { get; } = null!;
}

public class AttributeCollection : IEnumerable
{
    public IEnumerator GetEnumerator() => throw new NotImplementedException();
}

public class AttributeReference : Entity
{
    public string Tag { get; set; } = string.Empty;
    public string TextString { get; set; } = string.Empty;
}

public class AttributeDefinition : Entity
{
    public string Tag { get; set; } = string.Empty;
    public string TextString { get; set; } = string.Empty;
}

public class EntityColor
{
    public int ColorIndex { get; }
}

public class LayerTable : SymbolTable
{
    public override IEnumerator GetEnumerator() => throw new NotImplementedException();
}

public class LayerTableRecord : SymbolTableRecord
{
    public EntityColor? Color { get; }
    public bool IsFrozen { get; }
    public bool IsLocked { get; }
    public bool IsOff { get; }
    public bool IsPlottable { get; }
}

public class DimStyleTable : SymbolTable
{
    public override IEnumerator GetEnumerator() => throw new NotImplementedException();
}

public class DimStyleTableRecord : SymbolTableRecord
{
    public double Dimscale { get; }
    public double Dimtxt { get; }
}

public class DBDictionary : DBObject, IEnumerable
{
    public IEnumerator GetEnumerator() => throw new NotImplementedException();
}

public struct DBDictionaryEntry
{
    public string Key { get; }
    public ObjectId Value { get; }
}

public class Layout : DBObject
{
    public string LayoutName { get; } = string.Empty;
    public int TabOrder { get; }
}

public class TypedValue
{
    public TypedValue(int typeCode, object value) { TypeCode = typeCode; Value = value; }
    public int TypeCode { get; }
    public object Value { get; }
}

public class SelectionFilter
{
    public SelectionFilter(TypedValue[] filter) { }
}

public class LayoutManager
{
    private static readonly LayoutManager _current = new();
    public static LayoutManager Current => _current;
    public string CurrentLayout { get; } = string.Empty;
}
