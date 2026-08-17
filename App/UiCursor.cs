using System;
using System.Reflection;
using System.Windows.Input;

namespace FamilyPlanner
{
    static class UiCursor
    {
        static readonly Lazy<Cursor> nwse = new Lazy<Cursor>(delegate { return Load("FamilyPlanner.Assets.resize-nwse.cur"); });
        static readonly Lazy<Cursor> nesw = new Lazy<Cursor>(delegate { return Load("FamilyPlanner.Assets.resize-nesw.cur"); });
        static readonly Lazy<Cursor> horizontal = new Lazy<Cursor>(delegate { return Load("FamilyPlanner.Assets.resize-horizontal.cur"); });
        static readonly Lazy<Cursor> vertical = new Lazy<Cursor>(delegate { return Load("FamilyPlanner.Assets.resize-vertical.cur"); });

        public static Cursor ResizeNwSe { get { return nwse.Value; } }
        public static Cursor ResizeNeSw { get { return nesw.Value; } }
        public static Cursor ResizeHorizontal { get { return horizontal.Value; } }
        public static Cursor ResizeVertical { get { return vertical.Value; } }

        static Cursor Load(string name)
        {
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
            if (stream == null) return Cursors.Arrow;
            return new Cursor(stream);
        }
    }
}
