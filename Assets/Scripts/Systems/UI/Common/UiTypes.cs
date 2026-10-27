using UnityEngine;

namespace ProjectDM
{
    internal readonly struct RunUpgradePresentation
    {
        public readonly string upgrade;
        public readonly string icon;
        public readonly string category;
        public readonly string title;
        public readonly string description;
        public readonly Color accent;
        public readonly string grade;

        public RunUpgradePresentation(string upgrade, string icon, string category, string title, string description, Color accent, string grade = "bronze")
        {
            this.upgrade = upgrade;
            this.icon = icon;
            this.category = category;
            this.title = title;
            this.description = description;
            this.accent = accent;
            this.grade = grade;
        }
    }

    internal readonly struct MetaTreeNodePresentation
    {
        public readonly string id;
        public readonly string title;
        public readonly string subtitle;
        public readonly Vector2 position;
        public readonly int parentIndex;
        public readonly MetaTreeNodeState state;
        public readonly Color accent;

        public MetaTreeNodePresentation(string id, string title, string subtitle, Vector2 position, int parentIndex, MetaTreeNodeState state, Color accent)
        {
            this.id = id;
            this.title = title;
            this.subtitle = subtitle;
            this.position = position;
            this.parentIndex = parentIndex;
            this.state = state;
            this.accent = accent;
        }
    }

    internal enum MetaTreeNodeState { Completed, Reachable, Locked, Hidden }
}
