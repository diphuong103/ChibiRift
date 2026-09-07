using UnityEngine;
using UnityEngine.UI;
using ChibiRift.Data;

namespace ChibiRift.UI
{
    /// <summary>
    /// One upgrade card (SRS 19.3): icon, name, rarity and description, with a hover highlight.
    /// Reads <see cref="UpgradeData"/> straight from ChibiRift.Data, which UI is allowed to
    /// reference read-only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UpgradeCardView : MonoBehaviour
    {
        [Header("Content (UPG-002, UPG-007)")]
        [SerializeField] private Image _icon;
        [SerializeField] private Text _nameLabel;
        [SerializeField] private Text _descriptionLabel;
        [SerializeField] private Text _rarityLabel;
        [SerializeField] private Button _selectButton;

        [Header("Rarity colours (UPG-002)")]
        [Tooltip("One colour per rarity tier, in Rarity enum order.")]
        [SerializeField] private Color[] _rarityColors;

        /// <summary>The upgrade this card is showing.</summary>
        public UpgradeData Upgrade { get; private set; }

        /// <summary>Binds an upgrade to this card (UPG-002, UPG-007).</summary>
        public void Bind(UpgradeData upgrade)
        {
            // TODO(UPG-002): fill icon, name, description and rarity from the asset.
            // TODO(UPG-007): describe the actual effect, not just flavour text.
            Upgrade = upgrade;
        }
    }
}
