using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Battle
{
    public class StatusIconView : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private TextMeshProUGUI symbolText;
        [SerializeField] private TextMeshProUGUI turnsText;
        [SerializeField] private Color buffColor = new Color(0.23f, 0.35f, 0.74f, 0.82f);
        [SerializeField] private Color debuffColor = new Color(0.75f, 0.2f, 0.2f, 0.82f);
        [SerializeField] private Color specialColor = new Color(0.35f, 0.35f, 0.35f, 0.82f);

        public void Setup(ActiveStatus status)
        {
            symbolText.text = status.Definition.iconText;
            turnsText.text = status.RemainingTurns.ToString();

            if (status.Definition.category == "Buff")
                background.color = buffColor;
            else if (status.Definition.category == "Debuff")
                background.color = debuffColor;
            else
                background.color = specialColor;
        }
    }
}
