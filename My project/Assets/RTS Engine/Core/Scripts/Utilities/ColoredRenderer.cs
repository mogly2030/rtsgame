using RTSEngine.Entities;
using RTSEngine.Model;
using UnityEngine;

namespace RTSEngine.Utilities
{
    [System.Serializable]
    public struct ModelCacheAwareColoredRenderer
    {
        public Renderer renderer;
        public int materialID;

        [
            Range(0.0f, 1.0f),
            Tooltip(
                "How transparent would the color? The higher this value, the more transparent the color would be."
            )
        ]
        public float transparency;

        [
            Range(0.0f, 1.0f),
            Tooltip(
                "Adjust the darkness of the color, the higher this value, the darker the color would be."
            )
        ]
        public float darkness;

        public void UpdateColor(Color color, IEntity entity)
        {
            // Adjust brightness:
            Color.RGBToHSV(color, out float hue, out float saturation, out _);
            color = Color.HSVToRGB(hue, saturation, 1 - darkness);

            // Adjust transparency:
            color.a = 1.0f - transparency;

            if (renderer.IsValid())
            {
                if (!materialID.IsValidIndex(renderer.materials))
                {
                    RTSHelper.LoggingService.LogError(
                        $"[ColoredRenderer] Material ID {materialID} is invalid! Please follow error trace to fix the input of the material ID!"
                    );
                    return;
                }

                renderer.materials[materialID].SetColor("_Color", color);
            }
            else
                RTSHelper.LoggingService.LogError(
                    $"[ColoredRenderer - {entity.Code}] An element is either unassigned or assigned to an invalid child transform object! Please go through colored renderers and re-assign again.",
                    source: entity
                );
        }
    }

    [System.Serializable]
    public struct ColoredRenderer
    {
        public Renderer renderer;
        public int materialID;

        [Tooltip(
            "Name of the property in the material to color. Leave empty for '_Color'. In case of a shader graph material, check its property name in the shader!"
        )]
        public string colorPropertyName;

        [
            Range(0.0f, 1.0f),
            Tooltip(
                "How transparent would the color? The higher this value, the more transparent the color would be."
            )
        ]
        public float transparency;

        [
            Range(0.0f, 1.0f),
            Tooltip(
                "Adjust the darkness of the color, the higher this value, the darker the color would be."
            )
        ]
        public float darkness;

        public void UpdateColor(Color color, IEntity entity)
        {
            // Adjust brightness:
            Color.RGBToHSV(color, out float hue, out float saturation, out float value);
            color = Color.HSVToRGB(hue, saturation, 1 - darkness);

            // Adjust transparency:
            color.a = 1.0f - transparency;

            if (renderer.IsValid())
            {
                if (!materialID.IsValidIndex(renderer.materials))
                {
                    RTSHelper.LoggingService.LogError(
                        $"[ColoredRenderer] Material ID {materialID} is invalid! Please follow error trace to fix the input of the material ID!",
                        source: entity
                    );
                    return;
                }

                renderer
                    .materials[materialID]
                    .SetColor(
                        string.IsNullOrEmpty(colorPropertyName) ? "_Color" : colorPropertyName,
                        color
                    );
            }
            else
                RTSHelper.LoggingService.LogError(
                    $"[ColoredRenderer - {entity.Code}] An element is either unassigned or assigned to an invalid child transform object! Please go through colored renderers and re-assign again.",
                    source: entity
                );
        }
    }
}
