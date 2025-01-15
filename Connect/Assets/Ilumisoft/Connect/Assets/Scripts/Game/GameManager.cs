namespace Ilumisoft.Connect.Game
{
    using Ilumisoft.Connect;
    using Ilumisoft.Connect.Core;
    using System.Collections;
    using UnityEngine;
    using UnityEngine.UI;
    using TTSDK.UNBridgeLib.LitJson;
    using TTSDK;
    using StarkSDKSpace;
    using System.Collections.Generic;

    /// <summary>
    /// Handles the game flow
    /// </summary>
    public class GameManager : SingletonBehaviour<GameManager>
    {
        [SerializeField]
        private Button returnButton = null;

        /// <summary>
        /// The score of the player
        /// </summary>
        public static int Score { get; private set; }

        /// <summary>
        /// Reference to the game grid
        /// </summary>
        [SerializeField] private GameGrid grid = null;

        /// <summary>
        /// The number of moves the player has left
        /// </summary>
        [SerializeField] private int movesAvailable = 20;

        /// <summary>
        /// Gets or sets the  number of moves the player has left
        /// </summary>
        public int MovesAvailable
        {
            get => this.movesAvailable;
            set => this.movesAvailable = value;
        }


        public GameObject GameOverPanel;

        public string clickid;
        private StarkAdManager starkAdManager;

        /// <summary>
        /// Start listening to relevant events
        /// </summary>
        private void OnEnable()
        {
            GameEvents.OnElementsDespawned.AddListener(OnElementsDespawned);
        }

        //Stop listening from all events
        private void OnDisable()
        {
            GameEvents.OnElementsDespawned.RemoveListener(OnElementsDespawned);
        }

        /// <summary>
        /// Starts and processes the game flow
        /// </summary>
        /// <returns></returns>
        private IEnumerator Start()
        {
            this.returnButton.onClick.AddListener(OnBackButtonClick);

            InitializeGame();

            //Wait for the game to be executed completely
            yield return StartCoroutine(RunGame());

            //Wait for the game to finish
            yield return StartCoroutine(EndGame());
        }

        /// <summary>
        /// Returns to the menu scene
        /// </summary>
        protected void OnBackButtonClick()
        {
            SceneLoadingManager.Instance.LoadScene(SceneNames.Menu);
        }

        /// <summary>
        /// Check for escape button
        /// </summary>
        private void Update()
        {
            if (Input.GetKey(KeyCode.Escape))
            {
                OnBackButtonClick();
            }
        }

        /// <summary>
        /// Initilaizes the game and the grid
        /// </summary>
        public void InitializeGame()
        {
            Score = 0;

            this.grid.SetUpGrid();
        }

        /// <summary>
        /// Runs the game loop
        /// </summary>
        /// <returns></returns>
        public IEnumerator RunGame()
        {
            //Game Loop
            while (this.MovesAvailable > 0)
            {
                //Wait for the Player to select elements
                yield return this.grid.WaitForSelection();

                //Despawn selected elements
                yield return this.grid.DespawnSelection();

                //Wait for the grid elements to finish movement
                yield return this.grid.WaitForMovement();

                //Respawn despawned elements
                yield return this.grid.RespawnElements();
            }
        }

        /// <summary>
        /// Loads the game over scene
        /// </summary>
        /// <returns></returns>
        public IEnumerator EndGame()
        {
            yield return new WaitForSeconds(0.5f);
            
            GameOverPanel.SetActive(true);


            
        }
        public void LoadGameOver()
        {
            SceneLoadingManager.Instance.LoadScene(SceneNames.GameOver);
            ShowInterstitialAd("1lcaf5895d5l1293dc",
            () => {
                Debug.LogError("--插屏广告完成--");

            },
            (it, str) => {
                Debug.LogError("Error->" + str);
            });
        }
        public IEnumerator ContinueGame()
        {
            //继续游戏逻辑
            GameOverPanel?.SetActive(false);
            this.MovesAvailable += 10;
            //Wait for the game to be executed completely
            yield return StartCoroutine(RunGame());

            //Wait for the game to finish
            yield return StartCoroutine(EndGame());
        }
        public void Continue()
        {
            ShowVideoAd("192if3b93qo6991ed0",
            (bol) => {
                if (bol)
                {

                    StartCoroutine(ContinueGame());

                    clickid = "";
                    getClickid();
                    apiSend("game_addiction", clickid);
                    apiSend("lt_roi", clickid);


                }
                else
                {
                    StarkSDKSpace.AndroidUIManager.ShowToast("观看完整视频才能获取奖励哦！");
                }
            },
            (it, str) => {
                Debug.LogError("Error->" + str);
                //AndroidUIManager.ShowToast("广告加载异常，请重新看广告！");
            });
            
        }
        /// <summary>
        /// Gets invoked when the user has finished its move and 
        /// the selected elements are despawned
        /// </summary>
        /// <param name="count"></param>
        private void OnElementsDespawned(int count)
        {
            //Update score
            int oldScore = Score;
            Score = oldScore + count * (count - 1);

            //Invoke score changed event
            GameEvents.OnScoreChanged.Invoke(oldScore, Score);
        }


        public void getClickid()
        {
            var launchOpt = StarkSDK.API.GetLaunchOptionsSync();
            if (launchOpt.Query != null)
            {
                foreach (KeyValuePair<string, string> kv in launchOpt.Query)
                    if (kv.Value != null)
                    {
                        Debug.Log(kv.Key + "<-参数-> " + kv.Value);
                        if (kv.Key.ToString() == "clickid")
                        {
                            clickid = kv.Value.ToString();
                        }
                    }
                    else
                    {
                        Debug.Log(kv.Key + "<-参数-> " + "null ");
                    }
            }
        }

        public void apiSend(string eventname, string clickid)
        {
            TTRequest.InnerOptions options = new TTRequest.InnerOptions();
            options.Header["content-type"] = "application/json";
            options.Method = "POST";

            JsonData data1 = new JsonData();

            data1["event_type"] = eventname;
            data1["context"] = new JsonData();
            data1["context"]["ad"] = new JsonData();
            data1["context"]["ad"]["callback"] = clickid;

            Debug.Log("<-data1-> " + data1.ToJson());

            options.Data = data1.ToJson();

            TT.Request("https://analytics.oceanengine.com/api/v2/conversion", options,
               response => { Debug.Log(response); },
               response => { Debug.Log(response); });
        }


        /// <summary>
        /// </summary>
        /// <param name="adId"></param>
        /// <param name="closeCallBack"></param>
        /// <param name="errorCallBack"></param>
        public void ShowVideoAd(string adId, System.Action<bool> closeCallBack, System.Action<int, string> errorCallBack)
        {
            starkAdManager = StarkSDK.API.GetStarkAdManager();
            if (starkAdManager != null)
            {
                starkAdManager.ShowVideoAdWithId(adId, closeCallBack, errorCallBack);
            }
        }
        /// <summary>
        /// 播放插屏广告
        /// </summary>
        /// <param name="adId"></param>
        /// <param name="errorCallBack"></param>
        /// <param name="closeCallBack"></param>
        public void ShowInterstitialAd(string adId, System.Action closeCallBack, System.Action<int, string> errorCallBack)
        {
            starkAdManager = StarkSDK.API.GetStarkAdManager();
            if (starkAdManager != null)
            {
                var mInterstitialAd = starkAdManager.CreateInterstitialAd(adId, errorCallBack, closeCallBack);
                mInterstitialAd.Load();
                mInterstitialAd.Show();
            }
        }
    }
}