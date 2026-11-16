
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace VDF
{
    /// <summary>
    /// Dibuja un peldaño del diagrama y lo enciende según la corriente que lo recorre.
    ///
    /// Lo que se ilumina no es el peldaño entero de golpe: **cada tramo se enciende hasta
    /// donde llega la corriente**. Si el tercer contacto está abierto, los dos primeros
    /// tramos van en verde y el resto en gris. Eso es lo que convierte el diagrama en una
    /// explicación en vez de en un adorno: se ve *dónde* se corta.
    /// </summary>
    public class VdfLadderPeldanoVista : MonoBehaviour
    {
        [SerializeField] private List<Renderer> _tramos = new List<Renderer>();
        /// <summary>
        /// Las piezas de UN contacto: sus dos barras y, si es normalmente cerrado, la
        /// diagonal. Se agrupan porque hay que encenderlas juntas: registrando solo una, el
        /// símbolo se iluminaba a medias.
        /// </summary>
        [System.Serializable]
        private class PiezasContacto
        {
            public List<Renderer> Piezas = new List<Renderer>();
        }

        [SerializeField] private List<PiezasContacto> _piezasContacto = new List<PiezasContacto>();
        [SerializeField] private List<TextMeshPro> _etiquetas = new List<TextMeshPro>();
        [SerializeField] private List<Renderer> _piezasBobina = new List<Renderer>();
        [SerializeField] private TextMeshPro _etiquetaBobina;
        [SerializeField] private TextMeshPro _valorBloque;
        [SerializeField] private Renderer _barraProgreso;
        [SerializeField] private float _anchoBarra;

        /// <summary>Cuántos contactos hay en la serie, para saber qué tramo apagar.</summary>
        [SerializeField] private int _contactosEnSerie;

        /// <summary>Pinta el peldaño con el estado actual.</summary>
        public void Pintar(VdfLadderPeldano peldano, VdfEstadoLadder estado, VdfLadderPanel panel)
        {
            if (peldano == null || estado == null || panel == null) return;

            // La corriente avanza mientras los contactos conduzcan; en cuanto uno se abre,
            // todo lo que viene detrás se queda a oscuras.
            bool corriente = true;
            int indiceContacto = 0;

            for (int i = 0; i < peldano.Serie.Count; i++)
            {
                bool conduce;

                if (i == 0)
                {
                    conduce = peldano.Serie[0].Conduce(estado);
                    if (!conduce && peldano.Paralelo != null)
                        for (int j = 0; j < peldano.Paralelo.Count; j++)
                            if (peldano.Paralelo[j].Conduce(estado)) { conduce = true; break; }
                }
                else
                {
                    conduce = peldano.Serie[i].Conduce(estado);
                }

                PintarContacto(indiceContacto, corriente, peldano.Serie[i], estado, panel);
                indiceContacto++;

                corriente = corriente && conduce;
                PintarTramo(i, corriente, panel);
            }

            // La rama en paralelo se pinta aparte: tiene su propia condición.
            if (peldano.Paralelo != null)
            {
                for (int j = 0; j < peldano.Paralelo.Count; j++)
                {
                    PintarContacto(indiceContacto, true, peldano.Paralelo[j], estado, panel);
                    indiceContacto++;
                }
            }

            // Tramo final hasta la bobina.
            PintarTramo(peldano.Serie.Count, corriente, panel);
            PintarBobina(corriente, peldano.Bobina, estado, panel);
        }

        private void PintarTramo(int i, bool vivo, VdfLadderPanel panel)
        {
            if (i < 0 || i >= _tramos.Count || _tramos[i] == null) return;
            _tramos[i].sharedMaterial = panel.MaterialTramo(vivo);
        }

        private void PintarContacto(int i, bool corrienteLlega, VdfLadderElemento el,
                                    VdfEstadoLadder estado, VdfLadderPanel panel)
        {
            if (i < 0 || i >= _piezasContacto.Count) return;

            // Un contacto se dibuja encendido cuando CONDUCE, llegue o no corriente hasta
            // él: así se ve que un contacto está cerrado aunque el tramo anterior esté
            // cortado, que es información útil para entender dónde está el problema.
            bool conduce = el.Conduce(estado);
            var mat = panel.MaterialTramo(conduce);
            var grupo = _piezasContacto[i];
            if (grupo != null)
            {
                for (int k = 0; k < grupo.Piezas.Count; k++)
                    if (grupo.Piezas[k] != null) grupo.Piezas[k].sharedMaterial = mat;
            }

            if (i < _etiquetas.Count && _etiquetas[i] != null)
                _etiquetas[i].color = conduce ? panel.ColorTextoVivo : panel.ColorTexto;
        }

        private void PintarBobina(bool energizada, VdfLadderElemento bobina,
                                  VdfEstadoLadder estado, VdfLadderPanel panel)
        {
            for (int i = 0; i < _piezasBobina.Count; i++)
                if (_piezasBobina[i] != null)
                    _piezasBobina[i].sharedMaterial = panel.MaterialTramo(energizada);

            if (_etiquetaBobina != null)
                _etiquetaBobina.color = energizada ? panel.ColorTextoVivo : panel.ColorTexto;

            if (bobina == null) return;

            // Los bloques de temporizador y contador muestran su valor, que es lo que hace
            // visible la espera de 3 s en vez de dejarla como un retardo misterioso.
            if (_valorBloque != null)
            {
                if (bobina.Simbolo == VdfLadderSimbolo.Temporizador)
                    _valorBloque.text = estado.TextoTemporizador();
                else if (bobina.Simbolo == VdfLadderSimbolo.Contador)
                    _valorBloque.text = estado.TextoContador();
            }

            if (_barraProgreso != null && bobina.Simbolo == VdfLadderSimbolo.Temporizador)
            {
                float f = estado.ProgresoTemporizador();
                var t = _barraProgreso.transform;
                var s = t.localScale;
                s.x = Mathf.Max(0.0001f, _anchoBarra * f);
                t.localScale = s;
                // Crece desde la izquierda del bloque, no desde su centro.
                var p = t.localPosition;
                p.x = -_anchoBarra * 0.5f + s.x * 0.5f;
                t.localPosition = p;
                _barraProgreso.sharedMaterial = panel.MaterialTramo(f > 0f);
            }
        }

        // =====================================================================================
        // Construcción
        // =====================================================================================

        public static VdfLadderPeldanoVista Construir(Transform padre, VdfLadderPeldano peldano,
                                                      int indice, Vector3 posicion,
                                                      float ancho, float alto,
                                                      VdfLadderPanel panel)
        {
            var raiz = new GameObject("Peldano_" + (indice + 1));
            raiz.transform.SetParent(padre, false);
            raiz.transform.localPosition = posicion;

            var vista = raiz.AddComponent<VdfLadderPeldanoVista>();
            vista._contactosEnSerie = peldano.Serie.Count;

            const float grosorCable = 0.0035f;
            const float altoContacto = 0.016f;
            const float anchoContacto = 0.013f;

            // Reparto horizontal: los contactos de la serie, y la bobina al final.
            int n = peldano.Serie.Count;
            float xIni = -ancho * 0.5f;
            float xBobina = ancho * 0.5f - 0.055f;
            float paso = (xBobina - xIni) / (n + 0.5f);

            // Tramos de cable entre contactos.
            for (int i = 0; i <= n; i++)
            {
                float x0 = xIni + i * paso;
                float x1 = (i < n) ? x0 + paso : xBobina;
                float centro = (x0 + x1) * 0.5f;
                float largo = Mathf.Max(0.001f, x1 - x0);

                var tramo = VdfLadderPanel.Caja(raiz.transform, "Tramo_" + i,
                    new Vector3(centro, 0f, 0f),
                    new Vector3(largo, grosorCable, 0.003f),
                    panel.ColorMuerto, 0.3f, false);
                vista._tramos.Add(tramo.GetComponent<Renderer>());
            }

            // Contactos de la serie.
            for (int i = 0; i < n; i++)
            {
                float x = xIni + (i + 1) * paso;
                CrearContacto(vista, raiz.transform, peldano.Serie[i], x, 0f,
                              anchoContacto, altoContacto, grosorCable, panel);
            }

            // Rama en paralelo, bajo el primer contacto.
            if (peldano.Paralelo != null && peldano.Paralelo.Count > 0)
            {
                float yRama = -alto * 0.34f;
                float xPrimero = xIni + paso;

                // Las dos verticales que cierran la rama.
                VdfLadderPanel.Caja(raiz.transform, "RamaIzq",
                    new Vector3(xIni + 0.002f, yRama * 0.5f, 0f),
                    new Vector3(grosorCable, Mathf.Abs(yRama), 0.003f),
                    panel.ColorMuerto, 0.3f, false);
                VdfLadderPanel.Caja(raiz.transform, "RamaDer",
                    new Vector3(xPrimero + paso * 0.5f, yRama * 0.5f, 0f),
                    new Vector3(grosorCable, Mathf.Abs(yRama), 0.003f),
                    panel.ColorMuerto, 0.3f, false);
                VdfLadderPanel.Caja(raiz.transform, "RamaHoriz",
                    new Vector3((xIni + xPrimero + paso * 0.5f) * 0.5f, yRama, 0f),
                    new Vector3(xPrimero + paso * 0.5f - xIni, grosorCable, 0.003f),
                    panel.ColorMuerto, 0.3f, false);

                for (int j = 0; j < peldano.Paralelo.Count; j++)
                {
                    CrearContacto(vista, raiz.transform, peldano.Paralelo[j], xPrimero, yRama,
                                  anchoContacto, altoContacto, grosorCable, panel,
                                  etiquetaDebajo: true);
                }
            }

            CrearBobina(vista, raiz.transform, peldano.Bobina, xBobina, altoContacto, panel);

            // Título del peldaño, pequeño y a la izquierda del todo.
            var t = VdfLadderPanel.Texto(raiz.transform, "Titulo",
                new Vector3(0f, alto * 0.40f, -0.002f),
                new Vector2(ancho, 0.012f), 0.0072f,
                TextAlignmentOptions.Left, new Color(0.45f, 0.48f, 0.53f));
            t.text = (indice + 1) + ".  " + peldano.Titulo;

            return vista;
        }

        private static void CrearContacto(VdfLadderPeldanoVista vista, Transform padre,
                                          VdfLadderElemento el, float x, float y,
                                          float ancho, float alto, float grosor,
                                          VdfLadderPanel panel, bool etiquetaDebajo = false)
        {
            var nodo = new GameObject("Contacto_" + el.Etiqueta);
            nodo.transform.SetParent(padre, false);
            nodo.transform.localPosition = new Vector3(x, y, 0f);

            // Las dos barras verticales del símbolo.
            var izq = VdfLadderPanel.Caja(nodo.transform, "Barra_I",
                new Vector3(-ancho * 0.5f, 0f, 0f),
                new Vector3(grosor, alto, 0.003f), panel.ColorMuerto, 0.3f, false);
            var der = VdfLadderPanel.Caja(nodo.transform, "Barra_D",
                new Vector3(ancho * 0.5f, 0f, 0f),
                new Vector3(grosor, alto, 0.003f), panel.ColorMuerto, 0.3f, false);

            var grupo = new PiezasContacto();
            grupo.Piezas.Add(izq.GetComponent<Renderer>());
            grupo.Piezas.Add(der.GetComponent<Renderer>());

            // El normalmente cerrado lleva la diagonal que lo distingue. Es la diferencia
            // que el alumno tiene que ver de un vistazo: un NC conduce en reposo.
            if (el.Simbolo == VdfLadderSimbolo.ContactoNC)
            {
                var diag = VdfLadderPanel.Caja(nodo.transform, "Diagonal",
                    Vector3.zero,
                    new Vector3(ancho * 1.5f, grosor, 0.003f), panel.ColorMuerto, 0.3f, false);
                diag.transform.localRotation = Quaternion.Euler(0f, 0f, 38f);
                grupo.Piezas.Add(diag.GetComponent<Renderer>());
            }

            vista._piezasContacto.Add(grupo);

            // La etiqueta va encima del símbolo, salvo en la rama en paralelo, donde va al lado.
            //
            // Las dos vueltas que costó: la rama cuelga justo debajo del contacto de la serie,
            // así que con la etiqueta encima se leía «HABILIT.» montado sobre «START»; y
            // bajándola, lo que se pisaba era la etiqueta del peldaño siguiente, porque entre
            // peldaño y peldaño hay 5 cm y la etiqueta caía a 3,2. Al lado no choca con nada:
            // el tramo de la rama está vacío hasta el riel.
            // La z importa: a la altura de la rama pasa el propio cable, que es una caja de
            // 3 mm de grueso centrada en z=0, así que una etiqueta a -1 mm queda dentro de él
            // y se ve comida. Delante del cable se lee limpia.
            Vector3 posEtiqueta = etiquetaDebajo
                ? new Vector3(-ancho * 0.5f - 0.010f, 0f, -0.004f)
                : new Vector3(0f, alto * 0.95f, -0.001f);

            var etq = VdfLadderPanel.Texto(nodo.transform, "Etiqueta",
                posEtiqueta,
                new Vector2(0.075f, 0.010f), 0.0070f,
                etiquetaDebajo ? TextAlignmentOptions.Right : TextAlignmentOptions.Center,
                panel.ColorTexto);
            etq.text = el.Etiqueta;
            vista._etiquetas.Add(etq);
        }

        private static void CrearBobina(VdfLadderPeldanoVista vista, Transform padre,
                                        VdfLadderElemento bobina, float x, float alto,
                                        VdfLadderPanel panel)
        {
            if (bobina == null) return;

            var nodo = new GameObject("Bobina_" + bobina.Etiqueta);
            nodo.transform.SetParent(padre, false);
            nodo.transform.localPosition = new Vector3(x + 0.028f, 0f, 0f);

            const float grosor = 0.0035f;

            if (bobina.Simbolo == VdfLadderSimbolo.Bobina)
            {
                // Los dos paréntesis de la bobina, insinuados con barra y dos pestañas.
                for (int lado = -1; lado <= 1; lado += 2)
                {
                    var barra = VdfLadderPanel.Caja(nodo.transform, "Arco_" + lado,
                        new Vector3(lado * 0.011f, 0f, 0f),
                        new Vector3(grosor, alto * 0.85f, 0.003f), panel.ColorMuerto, 0.3f, false);
                    vista._piezasBobina.Add(barra.GetComponent<Renderer>());

                    for (int signo = -1; signo <= 1; signo += 2)
                    {
                        var pestana = VdfLadderPanel.Caja(nodo.transform, "Pest_" + lado + signo,
                            new Vector3(lado * 0.0075f, signo * alto * 0.42f, 0f),
                            new Vector3(0.008f, grosor, 0.003f), panel.ColorMuerto, 0.3f, false);
                        vista._piezasBobina.Add(pestana.GetComponent<Renderer>());
                    }
                }
            }
            else
            {
                // Temporizador y contador: un bloque rectangular con su valor dentro.
                const float anchoBloque = 0.072f;
                float altoBloque = alto * 1.5f;

                var marco = VdfLadderPanel.Caja(nodo.transform, "Bloque",
                    new Vector3(0.012f, 0f, 0f),
                    new Vector3(anchoBloque, altoBloque, 0.0035f),
                    new Color(0.16f, 0.18f, 0.21f), 0.2f, false);
                vista._piezasBobina.Add(marco.GetComponent<Renderer>());

                if (bobina.Simbolo == VdfLadderSimbolo.Temporizador)
                {
                    vista._anchoBarra = anchoBloque - 0.006f;
                    var barra = VdfLadderPanel.Caja(nodo.transform, "Progreso",
                        new Vector3(0.012f, -altoBloque * 0.33f, -0.002f),
                        new Vector3(0.0001f, 0.0035f, 0.002f), panel.ColorMuerto, 0.3f, false);
                    vista._barraProgreso = barra.GetComponent<Renderer>();
                }

                vista._valorBloque = VdfLadderPanel.Texto(nodo.transform, "Valor",
                    new Vector3(0.012f, altoBloque * 0.12f, -0.003f),
                    new Vector2(anchoBloque, 0.012f), 0.0072f,
                    TextAlignmentOptions.Center, panel.ColorTexto);
            }

            var etq = VdfLadderPanel.Texto(nodo.transform, "Etiqueta",
                new Vector3(0.006f, alto * 1.05f, -0.002f),
                new Vector2(0.085f, 0.011f), 0.0074f,
                TextAlignmentOptions.Center, panel.ColorTexto);
            etq.text = bobina.Etiqueta;
            vista._etiquetaBobina = etq;
        }
    }
}
