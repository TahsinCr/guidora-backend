using System.Collections.Generic;
using System.Linq;

namespace Business.Constants;

public static class ProductTaxonomySelectionPolicy
{
    public const int MaximumCategories = 20;
    public const int MaximumTags = 100;

    public static bool HasValidCategoryIds(IReadOnlyCollection<int>? ids) =>
        HasValidIds(ids, minimumCount: 1, MaximumCategories);

    public static bool HasValidTagIds(IReadOnlyCollection<int>? ids) =>
        HasValidIds(ids, minimumCount: 0, MaximumTags);

    private static bool HasValidIds(IReadOnlyCollection<int>? ids, int minimumCount, int maximumCount)
    {
        if (ids is null || ids.Count < minimumCount || ids.Count > maximumCount)
            return false;

        if (ids.Count == 0)
            return true;

        var distinct = new HashSet<int>();
        foreach (var id in ids)
        {
            if (id <= 0 || !distinct.Add(id))
                return false;
        }

        return true;
    }
}
