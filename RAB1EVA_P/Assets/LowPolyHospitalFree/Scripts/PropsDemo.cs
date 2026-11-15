using System.Collections;
using UnityEngine;

namespace HeyHeyThere.LowPolyHospitalFree
{
    /// <summary>
    /// The demo scene's showcase: the camera circles the props and every animated prop plays its
    /// animations in turn (a creature's through its Animator), with nothing to press. Remove it to use the scene as a plain layout.
    /// </summary>
    public class PropsDemo : MonoBehaviour
    {
        public Transform cameraRig;
        public float degreesPerSecond = 8f;
        public float pause = 1.5f;

#if UNITY_6000_6_OR_NEWER  // the sort mode is obsolete from 6.6, the overload without it new
        static T[] All<T>() where T : UnityEngine.Object => FindObjectsByType<T>();
#else
        static T[] All<T>() where T : UnityEngine.Object => FindObjectsByType<T>(FindObjectsSortMode.None);
#endif

        void Start()
        {
            foreach (var anim in All<Animation>())
                StartCoroutine(Play(anim));
            foreach (var animator in All<Animator>())
                if (animator.runtimeAnimatorController != null)
                    StartCoroutine(Play(animator));
        }

        void Update()
        {
            if (cameraRig != null)
                cameraRig.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.World);
        }

        IEnumerator Play(Animation anim)
        {
            yield return new WaitForSeconds(Random.Range(0f, pause));
            while (true)
                foreach (AnimationState state in anim)
                {
                    anim.Play(state.name);
                    yield return new WaitForSeconds(state.length + pause);
                }
        }

        /// <summary>A creature's controller, driven as a game would: it walks, attacks, is hit, dies and gets up.</summary>
        IEnumerator Play(Animator animator)
        {
            yield return new WaitForSeconds(Random.Range(0f, pause));
            while (true)
            {
                yield return new WaitForSeconds(pause);
                animator.SetFloat("Walk", 1f);
                yield return new WaitForSeconds(3f);
                animator.SetFloat("Walk", 0f);
                foreach (var trigger in new[] { "Attack", "Hit", "Die" })
                {
                    yield return new WaitForSeconds(pause);
                    animator.SetTrigger(trigger);
                    yield return new WaitForSeconds(0.25f);  // into its state
                    yield return new WaitForSeconds(animator.GetCurrentAnimatorStateInfo(0).length);
                }
                yield return new WaitForSeconds(pause);
                animator.Rebind();
            }
        }
    }
}
