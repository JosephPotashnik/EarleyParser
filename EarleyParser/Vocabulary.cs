using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EarleyParser
{
    /// <summary>
    /// Vocabulary class is responsible for storing all words with their possible Parts of Speech (POS).
    /// The corresponding production rules, PART-OF-SPEECH -> 'token', are kept separate from the Grammar class for purposes of clarity and functionality.
    /// Note: The grammar class allows lexicalized rules (e.g, A -> 'John', B -> 'John' 'left' , C -> 'John' D).
    /// 
    /// EarleyParser has access to the vocabulary in order to create the relevant Earley Items [PART-OF-SPEECH -> 'token', i, i] 
    /// in a pre-processing step, according to the input sentence.
    /// 
    /// example of a vocabulary may be found in Vocabulary.json
    /// </summary>
    public class Vocabulary
    {
        private static readonly JsonSerializerOptions s_jsonSerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        public Vocabulary()
        {
            WordWithPossiblePOS = [];
            POSWithPossibleWords = [];
        }

        // key = word, value = possible POS
        [JsonIgnore]
        public Dictionary<string, HashSet<string>> WordWithPossiblePOS { get; set; }

        // key = POS, value = words having the same POS.
        public Dictionary<string, HashSet<string>> POSWithPossibleWords { get; set; }

        [JsonIgnore]
        public HashSet<string> this[string word]
        {
            get
            {
                if (WordWithPossiblePOS.TryGetValue(word, out var value))
                {
                    return value;
                }

                return null;
            }
        }


        public static Vocabulary ReadVocabularyFromFile(string jsonFileName)
        {
            Vocabulary voc;
            jsonFileName = Path.Combine([".", "InputData", "Vocabularies", jsonFileName]);

            //deserialize JSON directly from a file
            using var file = File.OpenRead(jsonFileName);
            voc = JsonSerializer.Deserialize<Vocabulary>(file, s_jsonSerializerOptions);

            voc.PopulateDependentJsonPropertys();
            //voc.Disambiguate();

            Grammar.PartsOfSpeech = voc.POSWithPossibleWords.Keys.Select(x => x).ToHashSet();
            return voc;
        }

        public bool ContainsPOS(string pos) => POSWithPossibleWords.ContainsKey(pos);

        public void AddWordsToPOSCategory(string posCat, string[] words)
        {
            foreach (var word in words)
            {
                if (!WordWithPossiblePOS.TryGetValue(word, out var set))
                {
                    set = [];
                    WordWithPossiblePOS[word] = set;
                }

                set.Add(posCat);
            }

            if (!POSWithPossibleWords.TryGetValue(posCat, out var value))
            {
                value = [];
                POSWithPossibleWords[posCat] = value;
            }

            foreach (var word in words)
            {
                value.Add(word);
            }
        }


        //the function initializes WordWithPossiblePOS field after POSWithPossibleWords has been read from a json file.
        private void PopulateDependentJsonPropertys()
        {
            foreach (var kvp in POSWithPossibleWords)
            {
                var words = kvp.Value;
                foreach (var word in words)
                {
                    if (!WordWithPossiblePOS.TryGetValue(word, out var value))
                    {
                        value = [];
                        WordWithPossiblePOS[word] = value;
                    }

                    value.Add(kvp.Key);
                }
            }
        }

        public void Disambiguate()
        { 
                
            HashSet<string> ambiguousWords = new HashSet<string>();
            foreach (var kvp in POSWithPossibleWords)
            {
                var words = kvp.Value;
                var pos = kvp.Key;
                foreach (var word in words)
                {
                    if (WordWithPossiblePOS[word].Count > 1)
                    {
                        ambiguousWords.Add(word);
                    }
                 }
            }

            foreach (var word in ambiguousWords)
            {
                var poses = WordWithPossiblePOS[word];
                WordWithPossiblePOS.Remove(word);

                foreach (var pos in poses)
                {
                    POSWithPossibleWords[pos].Remove(word);
                }
            }
        }

        public HashSet<(string rhs1, string rhs2)> GetBigramsOfData(string[][] data)
        {
            var bigrams = new HashSet<(string rhs1, string rhs2)>();

            foreach (var words in data)
            {
                for (var i = 0; i < words.Length - 1; i++)
                {
                    var rhs1 = words[i];
                    var rhs2 = words[i + 1];

                    var possiblePOSForRHS1 = WordWithPossiblePOS[rhs1].ToArray();
                    var possiblePOSForRHS2 = WordWithPossiblePOS[rhs2].ToArray();

                    foreach (var pos1 in possiblePOSForRHS1)
                    {
                        foreach (var pos2 in possiblePOSForRHS2)
                        {
                            bigrams.Add((pos1, pos2));
                        }
                    }
                }
            }

            return bigrams;
        }
    }
}
