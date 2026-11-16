// NOTA IMPORTANTE DE UNITY
// Cada MonoBehaviour tiene que vivir en un archivo .cs con SU MISMO NOMBRE.
// Unity crea un unico MonoScript por archivo, con el nombre del archivo; cualquier
// MonoBehaviour cuya clase no coincida NO obtiene MonoScript y, por tanto, no se puede
// deserializar. Funciona dentro de la sesion (el tipo existe en memoria) y se rompe al
// reiniciar el editor, dejando "The referenced script is missing" en la escena.
// Ver E6 en VIABILIDAD.md: esto costo 71 componentes rotos y, probablemente, un crash.

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VDF
{
    /// <summary>
    /// Extremo de un cable banana que se puede agarrar con la mano y encajar en un borne.
    /// </summary>
    public class VdfXrPlug : MonoBehaviour
    {
        [Tooltip("Cable al que pertenece este extremo.")]
        public VdfXrCable Cable;

        [Tooltip("true = extremo A del cable, false = extremo B.")]
        public bool EsExtremoA;

        /// <summary>
        /// Borne en el que está enchufado ahora mismo, o null si está suelto.
        ///
        /// Lleva también la cuenta de puntas del borne (<see cref="VdfTerminal.Enchufados"/>).
        /// Antes la llevaba el socket, y las puntas que se enchufan sin socket —el cableado
        /// automático de las prácticas 4 y 5, y las apiladas— no contaban.
        /// </summary>
        public VdfTerminal Conectado
        {
            get { return _conectado; }
            set
            {
                if (_conectado == value) return;
                if (_conectado != null) _conectado.Enchufados--;
                _conectado = value;
                if (_conectado != null) _conectado.Enchufados++;
            }
        }
        private VdfTerminal _conectado;

        /// <summary>
        /// Sitio de la bandeja del que salió este conector.
        ///
        /// Hace falta para devolverlo: al empezar una práctica de cableado hay que dejar el
        /// módulo sin cables, y en VR «sin cables» no es vaciar una lista, es que los veinte
        /// cables vuelvan a estar sobre la mesa.
        /// </summary>
        public Vector3 Reposo;

        /// <summary>Punto por el que el cable sale de este conector.</summary>
        public Vector3 PuntoCable { get { return transform.position; } }
    }
}
