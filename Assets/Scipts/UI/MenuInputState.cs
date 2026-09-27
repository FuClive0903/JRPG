// Shared by direct keyboard handlers and the UI event module.
internal sealed class MenuInputState
{
    public object Owner { get; private set; }
    private int blockedFrame = -1;

    public bool IsBlocked(int frame) => frame == blockedFrame;
    public bool CanRead(object owner, int frame) => ReferenceEquals(Owner, owner) && !IsBlocked(frame);

    public void Activate(object owner, int frame)
    {
        Owner = owner;
        Block(frame);
    }

    public void Release(object owner, int frame)
    {
        if (!ReferenceEquals(Owner, owner)) return;
        Owner = null;
        Block(frame);
    }

    public void Block(int frame) => blockedFrame = frame;
}
