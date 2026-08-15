// Port of the abstract ElementChecker class at the bottom of lib/src/utils.dart

namespace HxGLTF.Validator.Internal;

/// <summary>
/// Dart <c>ElementChecker&lt;T extends num&gt;</c>. Values are delivered as double; checkers of integer
/// accessors cast to long where Dart printed an int.
/// </summary>
internal abstract class ElementChecker
{
    public virtual string Path => "";

    public abstract bool Check(Context context, int index, int componentIndex, double value);

    public virtual bool Done(Context context) => true;
}
