using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TSAGame
{
    /// <summary>
    /// The 3D game title. Each child is one letter; Play() flies them in one after another from deep space and
    /// eases each into its place. Letters are sorted by name (L00_T, L01_H, ...), which is how the model names them.
    /// </summary>
    public class TitleLetters : MonoBehaviour
    {
        [SerializeField] float flyTime = 1.7f;
        [SerializeField] float stagger = 0.16f;
        [SerializeField] float startDistance = 45f;

        readonly List<Transform> letters = new();
        Vector3[] homePositions;
        Quaternion[] homeRotations;

        void Awake()
        {
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>(true)) letters.Add(renderer.transform);
            letters.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            homePositions = new Vector3[letters.Count];
            homeRotations = new Quaternion[letters.Count];
            for (int i = 0; i < letters.Count; i++)
            {
                homePositions[i] = letters[i].position;
                homeRotations[i] = letters[i].rotation;
                letters[i].gameObject.SetActive(false);
            }
        }

        /// <summary>Flies every letter in. Finishes when the last one has landed.</summary>
        public IEnumerator Play()
        {
            for (int i = 0; i < letters.Count; i++) StartCoroutine(Fly(i, i * stagger));
            yield return new WaitForSeconds((letters.Count - 1) * stagger + flyTime);
        }

        IEnumerator Fly(int index, float delay)
        {
            yield return new WaitForSeconds(delay);
            var letter = letters[index];
            var home = homePositions[index];

            // Start far away, scattered, spinning; alternate sides so they cross as they arrive.
            var random = new System.Random(index * 7919 + 13);
            float side = index % 2 == 0 ? -1f : 1f;
            var offset = transform.right * (side * (0.4f + (float)random.NextDouble() * 0.5f))
                       + transform.up * ((float)random.NextDouble() - 0.5f)
                       - transform.forward * (0.8f + (float)random.NextDouble() * 0.6f);
            var start = home + offset * startDistance;
            var startRotation = homeRotations[index] * Quaternion.Euler(random.Next(-200, 200), random.Next(-300, 300), random.Next(-200, 200));

            letter.gameObject.SetActive(true);
            AudioManager.Play(Sfx.Throw, 0.35f, 0.25f);
            for (float t = 0; t < flyTime; t += Time.deltaTime)
            {
                float k = 1f - Mathf.Pow(1f - t / flyTime, 4f);       // fast at first, easing gently into place
                letter.SetPositionAndRotation(Vector3.Lerp(start, home, k), Quaternion.Slerp(startRotation, homeRotations[index], k));
                yield return null;
            }
            letter.SetPositionAndRotation(home, homeRotations[index]);
            AudioManager.Play(Sfx.Thud, 0.3f, 0.2f);
        }
    }
}
