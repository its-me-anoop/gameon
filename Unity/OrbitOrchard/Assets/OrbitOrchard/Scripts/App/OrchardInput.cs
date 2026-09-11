using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace OrbitOrchard.App
{
    /// <summary>Routes buffered mouse, touch and keyboard actions to runtime UI Toolkit panels.</summary>
    public static class OrchardInput
    {
        public static void Initialize(GameObject owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));

            var events = EventSystem.current;
            if (events == null) events = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            if (events == null)
            {
                var host = new GameObject("UI Input");
                host.SetActive(false);
                host.transform.SetParent(owner.transform, false);
                events = host.AddComponent<EventSystem>();
                var input = host.AddComponent<InputSystemUIInputModule>();
                input.AssignDefaultActions();
                // The module enables its UI actions in OnEnable and releases them
                // in OnDisable. Parenting also ties cleanup to the application.
                host.SetActive(true);
                return;
            }

            // Reuse a scene's existing event system, retaining any configured
            // Input System action asset and avoiding duplicate global dispatchers.
            var module = events.GetComponent<InputSystemUIInputModule>();
            if (module == null) module = events.gameObject.AddComponent<InputSystemUIInputModule>();
            foreach (var other in events.GetComponents<BaseInputModule>())
                if (other != module) other.enabled = false;
            if (module.actionsAsset == null) module.AssignDefaultActions();
            events.enabled = true;
            module.enabled = true;
        }
    }
}
