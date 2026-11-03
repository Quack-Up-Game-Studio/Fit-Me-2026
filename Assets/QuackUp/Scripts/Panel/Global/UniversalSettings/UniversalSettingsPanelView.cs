using System;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace FitMe.Panel
{
    public class UniversalSettingsPanelView : PanelView
    {
        [SerializeField] private TMP_Text gameVersionText;
        [SerializeField] private Button musicToggleButton;
        [SerializeField] private Image musicSlashImage;
        [SerializeField] private Button sfxToggleButton;
        [SerializeField] private Image sfxSlashImage;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button facebookButton;
        [SerializeField] private string facebookCommunityUrl;
        [SerializeField] private string backPanelId = "MainMenu";
        [SerializeField] private Button mainMenuButton;
        
        private UniversalSettingsPanelViewModel ViewModel => (UniversalSettingsPanelViewModel)BaseViewModel;
        private IDisposable _bindings;
#if FITME_PERFORMANCE_DIAGNOSTICS
        private Button _sharePerformanceLogButton;
#endif
        
        [Inject]
        public override void Construct(IPanelViewModel viewModel)
        {
            base.Construct(viewModel);
#if FITME_PERFORMANCE_DIAGNOSTICS
            CreatePerformanceLogShareButton();
#endif
            Bind();
        }

        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            ViewModel.BGMMuteState
                .Subscribe(OnMusicToggleStateChanged)
                .AddTo(ref disposableBuilder);
            ViewModel.SfxMuteState
                .Subscribe(OnSfxToggleStateChanged)
                .AddTo(ref disposableBuilder);
            musicToggleButton.OnClickAsObservable()
                .Subscribe(_ => OnMusicToggleButtonClicked())
                .AddTo(ref disposableBuilder);
            sfxToggleButton.OnClickAsObservable()
                .Subscribe(_ => OnSfxToggleButtonClicked())
                .AddTo(ref disposableBuilder);
            closeButton.OnClickAsObservable()
                .Subscribe(_ => OnCloseButtonClicked())
                .AddTo(ref disposableBuilder);
            
            facebookButton.OnClickAsObservable()
                .Subscribe(_ => OnFacebookButtonClicked())
                .AddTo(ref disposableBuilder);
#if FITME_PERFORMANCE_DIAGNOSTICS
            if (_sharePerformanceLogButton)
            {
                _sharePerformanceLogButton.OnClickAsObservable()
                    .Subscribe(_ => ViewModel.SharePerformanceLogCommand.Execute(Unit.Default))
                    .AddTo(ref disposableBuilder);
            }
#endif

            if (mainMenuButton)
            {
                mainMenuButton.OnClickAsObservable()
                    .Subscribe(_ => OnMainMenuButtonClicked())
                    .AddTo(ref disposableBuilder);
            }
            
            _bindings = disposableBuilder.Build();
        }

#if FITME_PERFORMANCE_DIAGNOSTICS
        private void CreatePerformanceLogShareButton()
        {
            if (Application.platform != RuntimePlatform.Android) return;

            var shareButtonObject = new GameObject(
                "SharePerformanceLogButton",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            shareButtonObject.transform.SetParent(transform, false);

            var buttonRect = (RectTransform)shareButtonObject.transform;
            buttonRect.anchorMin = new Vector2(0.5f, 0f);
            buttonRect.anchorMax = new Vector2(0.5f, 0f);
            buttonRect.pivot = new Vector2(0.5f, 0f);
            buttonRect.anchoredPosition = new Vector2(0f, 100f);
            buttonRect.sizeDelta = new Vector2(520f, 120f);

            var image = shareButtonObject.GetComponent<Image>();
            image.color = new Color(0.16f, 0.34f, 0.53f, 1f);
            var shareButton = shareButtonObject.GetComponent<Button>();
            shareButton.targetGraphic = image;

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(shareButtonObject.transform, false);
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var label = labelObject.GetComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.fontSize = 32f;
            label.alignment = TextAlignmentOptions.Center;
            label.text = "Share Performance Log";
            _sharePerformanceLogButton = shareButton;
        }
#endif

        protected override void OnVisibilityStateChanged(VisibilityState state)
        {
            base.OnVisibilityStateChanged(state);
            if (state == VisibilityState.Hidden) return;
            if (!gameVersionText) return;
            gameVersionText.text = $"v{Application.version}";
        }

        public override void Dispose()
        {
            base.Dispose();
            _bindings?.Dispose();
        }
        
        private void OnMusicToggleStateChanged(bool isMuted)
        {
            if (!musicSlashImage) return;
            musicSlashImage.enabled = isMuted;
        }
        
        private void OnSfxToggleStateChanged(bool isMuted)
        {
            if (!sfxSlashImage) return;
            sfxSlashImage.enabled = isMuted;
        }
        
        private void OnMusicToggleButtonClicked()
        {
            ViewModel.BGMMuteState.Value = !ViewModel.BGMMuteState.Value;
        }
        
        private void OnSfxToggleButtonClicked()
        {
            ViewModel.SfxMuteState.Value = !ViewModel.SfxMuteState.Value;
        }

        private void OnCloseButtonClicked()
        {
            ViewModel.ApplyChangesCommand.Execute(Unit.Default);
            if (!TryGetCrossfadeRule(backPanelId, out var rule)) return;
            ViewModel.CrossfadeCommand.Execute(new CrossfadeCommandData(backPanelId, rule.crossfadeSettings));
        }

        private void OnFacebookButtonClicked()
        {
            if (string.IsNullOrEmpty(facebookCommunityUrl)) return;
            Application.OpenURL(facebookCommunityUrl);
        }

        private void OnMainMenuButtonClicked()
        {
            ViewModel.ApplyChangesCommand.Execute(Unit.Default);
            ViewModel.ToMainMenuCommand.Execute(Unit.Default);
        }
    }
}
