using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class ApiManager : MonoBehaviour
{
    public Text playerListText; // Reference to the UI Text component
    private string authApiUrl = "http://20.15.114.131:8080/api/login";
    private string playerListApiUrl = "http://20.15.114.131:8080/api/user/profile/list";
    private string apiUrl = "http://20.15.114.131:8080/api/power-consumption/current/view";
    private string apiKey = "NjVkNDIyMjNmMjc3NmU3OTI5MWJmZGI4OjY1ZDQyMjIzZjI3NzZlNzkyOTFiZmRhZQ";
    private string token;
    private List<Player> playerList = new List<Player>();
    private float interval = 10f;
    private float previousValue = 0f;
    private bool isFirstRun = true;
    private float threshold = 1f; // Set threshold to 1 Watt-hour
    private string targetUsername = "oversight_g19"; // Set this to the username you want to update

    void Start()
    {
        StartCoroutine(AuthenticateAndFetchPlayerList());
    }

    IEnumerator AuthenticateAndFetchPlayerList()
    {
        Debug.Log("Starting authentication...");
        yield return StartCoroutine(AuthenticateAndFetchProfile());

        if (!string.IsNullOrEmpty(token))
        {
            UnityWebRequest listRequest = UnityWebRequest.Get(playerListApiUrl);
            listRequest.SetRequestHeader("Authorization", "Bearer " + token);
            listRequest.SetRequestHeader("X-API-KEY", apiKey);

            yield return listRequest.SendWebRequest();

            if (listRequest.result == UnityWebRequest.Result.Success)
            {
                string listResponseText = listRequest.downloadHandler.text;
                playerList = ExtractPlayerListFromJson(listResponseText);
                AssignScoresToPlayers();
                // Ensure initial sorting and displaying of the player list
                playerList = playerList.OrderByDescending(p => p.score).ToList();
                DisplayPlayerList();

                // Start calling the API periodically to update player scores
                StartCoroutine(CallApiPeriodically());
            }
            else
            {
                Debug.LogError("Failed to fetch player list: " + listRequest.error);
            }
        }
    }

    IEnumerator AuthenticateAndFetchProfile()
    {
        using (UnityWebRequest www = UnityWebRequest.Post(authApiUrl, new WWWForm()))
        {
            www.SetRequestHeader("Content-Type", "application/json");
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes("{\"apiKey\": \"" + apiKey + "\"}");
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                string jsonResponse = www.downloadHandler.text;
                TokenResponse response = JsonUtility.FromJson<TokenResponse>(jsonResponse);
                token = response.token;
                Debug.Log("Token fetched: " + token);
            }
            else
            {
                Debug.LogError("Failed to fetch token: " + www.error);
            }
        }
    }

    List<Player> ExtractPlayerListFromJson(string jsonResponse)
    {
        PlayerListResponse playerListResponse = JsonUtility.FromJson<PlayerListResponse>(jsonResponse);
        return playerListResponse != null && playerListResponse.userViews != null ? playerListResponse.userViews : new List<Player>();
    }

    void AssignScoresToPlayers()
    {
        foreach (var player in playerList)
        {
            player.score = Random.Range(1, 101);
        }
    }

    IEnumerator CallApiPeriodically()
    {
        while (true)
        {
            yield return StartCoroutine(CallApi());
            yield return new WaitForSeconds(interval);
        }
    }

    IEnumerator CallApi()
    {
        using (UnityWebRequest webRequest = UnityWebRequest.Get(apiUrl))
        {
            webRequest.SetRequestHeader("Authorization", "Bearer " + token);
            webRequest.SetRequestHeader("X-API-KEY", apiKey);
            webRequest.SetRequestHeader("Content-Type", "application/json");

            yield return webRequest.SendWebRequest();

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Error: " + webRequest.error);
            }
            else
            {
                ProcessApiResponse(webRequest.downloadHandler.text);
            }
        }
    }

    void ProcessApiResponse(string jsonResponse)
    {
        ApiResponse response = JsonUtility.FromJson<ApiResponse>(jsonResponse);
        float currentConsumption = response.currentConsumption;

        Debug.Log("API Response Value: " + currentConsumption);

        if (isFirstRun)
        {
            previousValue = currentConsumption;
            isFirstRun = false;
            Debug.Log("Initial Value Set: " + previousValue);
        }
        else
        {
            float gradient = currentConsumption - previousValue;
            Debug.Log("Current Value: " + currentConsumption);
            Debug.Log("Previous Value: " + previousValue);
            Debug.Log("Gradient: " + gradient);

            if (Mathf.Abs(gradient) > threshold)
            {
                UpdatePlayerScore(targetUsername, -5); // Decrease score by 5
            }
            else
            {
                UpdatePlayerScore(targetUsername, 5); // Increase score by 5
            }

            previousValue = currentConsumption;
        }
    }

    void UpdatePlayerScore(string username, float scoreChange)
    {
        Player player = playerList.FirstOrDefault(p => p.username == username);
        if (player != null)
        {
            float newScore = player.score + scoreChange;
            // Ensure the score remains within the range of 0 to 100
            newScore = Mathf.Clamp(newScore, 0f, 100f);
            
            player.score = newScore;
            playerList = playerList.OrderByDescending(p => p.score).ToList();
            DisplayPlayerList();
        }
    }

    void DisplayPlayerList()
    {
        string playerListString = "";
        int rank = 1;
        foreach (var player in playerList)
        {
            playerListString += $"Rank: {rank}, Username: {player.username}, Score: {player.score}\n";
            rank++;
        }
        Debug.Log("Player List String: " + playerListString);
        playerListText.text = playerListString;
    }
}

[System.Serializable]
public class TokenResponse
{
    public string token;
}

[System.Serializable]
public class PlayerListResponse
{
    public List<Player> userViews;
}

[System.Serializable]
public class Player
{
    public string username;
    public float score;
}

[System.Serializable]
public class ApiResponse
{
    public float currentConsumption;
}
