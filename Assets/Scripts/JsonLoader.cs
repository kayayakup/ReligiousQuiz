using UnityEngine;

/// <summary>
/// Helper class that loads the questions JSON from Resources.
/// Place your "questions" TextAsset inside  Assets/Resources/questions.json
/// </summary>
namespace MillionaireGame
{
    public static class JsonLoader
    {
        /// <summary>
        /// Loads and deserializes the question database from Resources/questions.json.
        /// Returns null if the file is missing or malformed.
        /// </summary>
        public static QuestionDatabase LoadQuestions(string fileName)
        {
            TextAsset textAsset = Resources.Load<TextAsset>(fileName);

            if (textAsset == null)
            {
                Debug.LogError("[JsonLoader] Could not find Resources/questions.json!");
                return null;
            }

            // Unity's JsonUtility cannot deserialize a raw array,
            // so the JSON file must wrap the array: { "questions": [ ... ] }
            QuestionDatabase db = JsonUtility.FromJson<QuestionDatabase>(textAsset.text);

            if (db == null || db.questions == null || db.questions.Count == 0)
            {
                Debug.LogError("[JsonLoader] JSON parsed but question list is empty!");
                return null;
            }

            Debug.Log($"[JsonLoader] Loaded {db.questions.Count} questions from JSON.");
            return db;
        }
    }
}
