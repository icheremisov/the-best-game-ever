using UnityEngine;

namespace Mimic.UI
{
    // Фикс. 1920x1080 контейнер UIStage — родитель для окон (аналог Stage для мира).
    // Окна, лежащие в нём, всегда вписаны в композицию и леттербоксятся как фон/поле.
    public static class UiStageRoot
    {
        private static Transform cached;

        public static Transform For(Canvas fallback)
        {
            if (cached != null) return cached;
            var go = GameObject.Find("Canvas/UIStage");
            if (go != null) cached = go.transform;
            if (cached != null) return cached;
            return fallback != null ? fallback.transform : null;
        }
    }
}
