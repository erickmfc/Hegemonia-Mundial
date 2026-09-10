namespace Hegemonia.AI.IA01
{
    internal static class IA01_CityIntentAdapter
    {
        public static bool IsCityIntent(IA01IntentType intent)
        {
            return intent == IA01IntentType.BuildResidentialCapacity
                || intent == IA01IntentType.BuildStarterHouse
                || intent == IA01IntentType.BuildMediumApartment
                || intent == IA01IntentType.BuildHighApartment;
        }
    }
}

namespace Hegemonia.AI.IA02
{
    internal static class IA02_CityIntentAdapter
    {
        public static bool IsCityIntent(IA02IntentType intent)
        {
            return intent == IA02IntentType.BuildResidentialCapacity
                || intent == IA02IntentType.BuildStarterHouse
                || intent == IA02IntentType.BuildMediumApartment
                || intent == IA02IntentType.BuildHighApartment;
        }
    }
}
