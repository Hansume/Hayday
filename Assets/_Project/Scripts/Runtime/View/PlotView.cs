using FarmGame.Farming;
using UnityEngine;

namespace FarmGame.View
{
    public sealed class PlotView : MonoBehaviour
    {
        private SpriteRenderer _cropRenderer;
        private string _cropId;
        private int _stageIndex = -1;
        private PlotPhase _phase = PlotPhase.Empty;

        public void Initialize(SpriteRenderer cropRenderer)
        {
            _cropRenderer = cropRenderer;
            _cropRenderer.enabled = false;
        }

        public void Refresh(PlotSnapshot snapshot)
        {
            if (_cropRenderer == null)
            {
                return;
            }

            if (snapshot.IsEmpty || snapshot.Crop == null)
            {
                _cropRenderer.enabled = false;
                _cropId = null;
                _stageIndex = -1;
                _phase = PlotPhase.Empty;
                return;
            }

            _cropRenderer.enabled = true;

            if (_cropId != snapshot.Crop.Id || _stageIndex != snapshot.StageIndex)
            {
                _cropRenderer.sprite = snapshot.Crop.GetStageSprite(snapshot.StageIndex, snapshot.StageCount - 1);
                _cropId = snapshot.Crop.Id;
                _stageIndex = snapshot.StageIndex;
            }

            if (_phase != snapshot.Phase)
            {
                _phase = snapshot.Phase;
                _cropRenderer.transform.localScale = Vector3.one;
            }

            if (snapshot.IsReady)
            {
                var wobble = 1f + 0.035f * Mathf.Sin(Time.time * 4f);
                _cropRenderer.transform.localScale = new Vector3(wobble, wobble, 1f);
            }
            else
            {
                _cropRenderer.transform.localScale = Vector3.one;
            }
        }
    }
}
