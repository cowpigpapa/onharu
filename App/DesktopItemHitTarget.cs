using System;
using System.Windows;

namespace FamilyPlanner
{
    // Carries the exact calendar segment that Explorer-layer hit testing needs.
    // Keeping this outside MainWindow separates hit-test data from UI state.
    sealed class ItemHitTarget
    {
        public PlannerItem Item;
        public DateTime SegmentStart;
        public DateTime SegmentEnd;
        public FrameworkElement Element;
        public bool DetailCard;
    }

    sealed class DetailGroupHitTarget
    {
        public string GroupKey;
    }
}
