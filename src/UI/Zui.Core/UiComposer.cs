using System.Runtime.CompilerServices;

namespace ZEngine.UI;

public sealed class UiComposer<TTheme>
    where TTheme : SysTheme
{
    private readonly Stack<Frame> _frames = new();
    private readonly TTheme _theme;
    private readonly UiNode _root = new(new(1), UiTag.Root);

    public UiComposer(TTheme theme)
    {
        _theme = theme;
        _frames.Push(new(_root));
    }

    public TTheme Theme => _theme;

    public UiNode Add(
        UiTag tag,
        string? key,
        string callerFile,
        int callerLine)
    {
        Frame parent = _frames.Peek();
        int sequence = parent.NextSequence++;
        UiNodeId id = new(StableHash(
            parent.Node.Id.Value,
            tag,
            key,
            callerFile,
            callerLine,
            sequence));
        UiNode node = new(id, tag) { Key = key };
        parent.Node.Children.Add(node);
        return node;
    }

    public IDisposable Enter(UiNode node)
    {
        _frames.Push(new(node));
        return new PopScope(_frames);
    }

    public UiNode Root => _root;

    private static ulong StableHash(
        ulong parent,
        UiTag tag,
        string? key,
        string callerFile,
        int callerLine,
        int sequence)
    {
        const ulong prime = 1099511628211;
        ulong hash = 14695981039346656037;
        Add(parent);
        Add((ulong)tag);
        Add((ulong)callerLine);
        Add((ulong)sequence);
        foreach (char value in callerFile)
        {
            hash ^= value;
            hash *= prime;
        }

        if (key is not null)
        {
            foreach (char value in key)
            {
                hash ^= value;
                hash *= prime;
            }
        }

        return hash;

        void Add(ulong value)
        {
            hash ^= value;
            hash *= prime;
        }
    }

    private sealed class Frame(UiNode node)
    {
        public UiNode Node { get; } = node;

        public int NextSequence;
    }

    private sealed class PopScope(Stack<Frame> frames) : IDisposable
    {
        public void Dispose() => _ = frames.Pop();
    }
}

public static class UiTreeDiffer
{
    public static UiTreeChange Compare(UiNode? previous, UiNode current)
    {
        if (previous is null)
        {
            return new(Count(current), 0, 0, false);
        }

        Dictionary<UiNodeId, UiNode> oldNodes = [];
        Dictionary<UiNodeId, UiNode> newNodes = [];
        Flatten(previous, oldNodes);
        Flatten(current, newNodes);
        int added = newNodes.Keys.Count(id => !oldNodes.ContainsKey(id));
        int removed = oldNodes.Keys.Count(id => !newNodes.ContainsKey(id));
        int updated = newNodes.Count(pair =>
            oldNodes.TryGetValue(pair.Key, out UiNode? old)
            && !Equivalent(old, pair.Value));
        return new(added, removed, updated, false);
    }

    private static bool Equivalent(UiNode left, UiNode right) =>
        left.Tag == right.Tag
        && left.Text == right.Text
        && left.Value == right.Value
        && left.Placeholder == right.Placeholder
        && left.Role == right.Role
        && left.SemanticLabel == right.SemanticLabel
        && left.Style == right.Style;

    private static int Count(UiNode node) =>
        1 + node.Children.Sum(Count);

    private static void Flatten(
        UiNode node,
        Dictionary<UiNodeId, UiNode> destination)
    {
        destination[node.Id] = node;
        foreach (UiNode child in node.Children)
        {
            Flatten(child, destination);
        }
    }
}
