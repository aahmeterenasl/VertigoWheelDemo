using System;
using UnityEngine;
using VertigoWheel.Core.Randomness;
using VertigoWheel.Core.Runs;
using VertigoWheel.Settings;
using VertigoWheel.UI.Board;
using VertigoWheel.UI.Dialogs;

namespace VertigoWheel.App
{
    /// <summary>Composes and disposes the scene's session, random sources, controller, and view references.</summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField]
        private GameSettings _settings;

        [SerializeField]
        private GameView _view;

        [SerializeField]
        private RewardRevealView _rewardReveal;

        [SerializeField]
        private ExitConfirmationView _exitConfirmation;

        [SerializeField]
        private BombResultView _bombResult;

        [SerializeField]
        private CashOutSummaryView _cashOutSummary;

        private RunSession _session;
        private GameController _controller;

        public RunSession Session => _session;
        public GameController Controller => _controller;

        private void OnEnable()
        {
            try
            {
                ValidateReferences();
                _settings.Validate();
                _session = new RunSession(_settings, new SystemRandomSource(), new SystemRandomSource());
                _controller = new GameController(_settings, _session, _view,
                    _rewardReveal, _exitConfirmation, _bombResult, _cashOutSummary);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                if (_view != null)
                {
                    _view.SetBoardBlocked(true);
                    _view.SetStatus(exception.Message);
                }
                Debug.LogError(exception.Message, this);
            }
        }

        private void Update() => _controller?.RefreshReadyState();

        private void OnApplicationPause(bool paused)
        {
            if (!paused)
            {
                _controller?.Resume();
            }
        }

        private void OnDisable()
        {
            _controller?.Dispose();
            _controller = null;
            _session = null;
        }

        private void ValidateReferences()
        {
            if (_settings == null || _view == null || _rewardReveal == null
                || _exitConfirmation == null || _bombResult == null || _cashOutSummary == null)
            {
                throw new InvalidOperationException("The game bootstrap needs settings, board, and all four result dialog references.");
            }
        }
    }
}
