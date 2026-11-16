using System.Collections.Generic;

namespace VDF
{
    public static class VdfCatalogoFallas
    {
        public static List<VdfFallaDidactica> Crear()
        {
            return Crear(VdfMando.Activo);
        }

        /// <summary>
        /// Las seis averías del modo de mando dado. Con interruptores (la práctica del
        /// laboratorio) cambian dos, porque cambian el cableado y la configuración: la marcha
        /// llega por S4, así que la avería de «no arranca» es H1-04 y no H1-01, y el común de
        /// las entradas es el cable 2L → SC en vez de GND → SC.
        /// </summary>
        public static List<VdfFallaDidactica> Crear(VdfModoMando modo)
        {
            bool lab = modo == VdfModoMando.Interruptores;

            var noArranca = lab
                ? VdfFallaDidactica.DeParametro(
                    "H1-04_RESET",
                    "El motor no arranca de ninguna manera",
                    "H1-04", 14f, 40f,
                    sintoma:
                        "El tablero tiene tensión y los pilotos V1 a V4 se encienden al " +
                        "mover los interruptores. Pero el motor no se mueve y el variador " +
                        "marca 0,00 Hz pase lo que pase.",
                    explicacion:
                        "H1-04 = 14 deja el terminal S4 como reset de falla, que es su valor " +
                        "de fábrica. En esta práctica la marcha del PLC sale por Q0.5 y llega " +
                        "a S4, así que el PLC está pidiendo «resetea» en vez de «arranca». El " +
                        "valor correcto es 40 (marcha adelante en secuencia de 2 hilos).\n" +
                        "CÓMO REPARARLO: en el teclado del variador, H1-04 = 40.")
                : VdfFallaDidactica.DeParametro(
                    "H1-01_LOCAL_REMOTE",
                    "El motor no arranca de ninguna manera",
                    "H1-01", 1f, 40f,
                    sintoma:
                        "El tablero tiene tensión, el PLC se habilita y los indicadores " +
                        "V1 a V4 van cambiando al cambiar de velocidad. Pero el motor " +
                        "no se mueve y el variador marca 0,00 Hz pase lo que pase.",
                    explicacion:
                        "H1-01 = 1 asigna al terminal S1 la función LOCAL/REMOTE, que NO es " +
                        "una orden de marcha. El PLC está cerrando S1 correctamente, pero el " +
                        "variador lo interpreta como «cambia de modo», no como «arranca». El " +
                        "valor correcto es 40 (marcha adelante en secuencia de 2 hilos).\n" +
                        "CÓMO REPARARLO: en el teclado del variador, H1-01 = 40.");

            var sinComun = lab
                ? VdfFallaDidactica.DeCable(
                    "SC_SIN_COMUN",
                    "Ninguna entrada del variador hace efecto",
                    "PLC-2L", "SC",
                    sintoma:
                        "El PLC se comporta con normalidad y sus salidas conmutan, pero el " +
                        "variador no reacciona a ninguna de ellas: ni arranca ni cambia de " +
                        "velocidad. Es como si S4, S5 y S6 no estuvieran conectadas.",
                    explicacion:
                        "Falta el cable entre el común 2L del PLC y SC, el común de las " +
                        "entradas digitales del variador. Las salidas Q0.5 a Q0.7 son " +
                        "contactos de relé: cierran el circuito entre su borne y 2L. Sin 2L " +
                        "unido a SC, el contacto se cierra pero el circuito no, así que la " +
                        "entrada nunca se activa.\n" +
                        "Es un fallo clásico porque el cable del común no va a ninguna " +
                        "función concreta y es el que más fácil se olvida.\n" +
                        "CÓMO REPARARLO: vuelve a enchufar el cable suelto entre 2L del PLC y SC.")
                : VdfFallaDidactica.DeCable(
                    "SC_SIN_COMUN",
                    "Ninguna entrada del variador hace efecto",
                    "GND24", "SC",
                    sintoma:
                        "El PLC se comporta con normalidad y sus salidas conmutan, pero el " +
                        "variador no reacciona a ninguna de ellas: ni arranca ni cambia de " +
                        "velocidad. Es como si S1, S5 y S6 no estuvieran conectadas.",
                    explicacion:
                        "Falta el cable entre GND (el 0 V de la fuente de 24 V) y SC, el común " +
                        "de las entradas digitales. " +
                        "Sin común no hay circuito que cerrar: la salida del PLC lleva " +
                        "tensión a S1, pero esa tensión no tiene por dónde volver, así que " +
                        "la entrada nunca se activa.\n" +
                        "Es un fallo clásico porque el cable del común no va a ninguna " +
                        "función concreta y es el que más fácil se olvida.\n" +
                        "CÓMO REPARARLO: vuelve a enchufar el cable suelto entre GND (junto a " +
                        "VCC 24V) y SC.");

            return new List<VdfFallaDidactica>
            {
                // --- El error estrella del manual del módulo, o su versión del laboratorio
                noArranca,

                // --- El otro error del manual, mucho más sutil ------------------------------
                VdfFallaDidactica.DeIntercambio(
                    "H1-05_H1-06_CRUZADOS",
                    "Las velocidades 2 y 3 salen cambiadas",
                    "H1-05", 3f,
                    "H1-06", 4f,
                    sintoma:
                        "Todo funciona: el motor arranca, las cuatro velocidades responden y " +
                        "los indicadores se encienden donde toca. Pero al pasar de la " +
                        "velocidad 1 a la 2 el motor va a 35 Hz en vez de a 25, y en la 3 va " +
                        "a 25 en vez de a 35.",
                    explicacion:
                        "H1-05 y H1-06 están intercambiados. S5 debería ser multi-velocidad 1 " +
                        "(el bit bajo) y S6 multi-velocidad 2 (el bit alto). Al cruzarlos, el " +
                        "número binario que forman los dos bits se lee al revés, así que las " +
                        "combinaciones 01 y 10 se permutan: d1-02 y d1-03 cambian de sitio.\n" +
                        "Las velocidades 1 y 4 no se notan porque sus bits son 00 y 11, que " +
                        "son simétricos. Por eso esta avería es difícil de ver.\n" +
                        "CÓMO REPARARLO: en el teclado del variador, H1-05 = 3 y H1-06 = 4."),

                // --- Referencia de frecuencia: un fallo PARCIAL -----------------------------
                VdfFallaDidactica.DeParametro(
                    "b1-01_ANALOGICA",
                    "Solo la velocidad 1 se queda en cero",
                    "b1-01", 1f, 0f,
                    sintoma:
                        "Las velocidades 2, 3 y 4 funcionan perfectamente. Pero en la " +
                        "velocidad 1 el motor no gira: el variador marca 0,00 Hz, aunque el " +
                        "indicador V1 esté encendido.",
                    explicacion:
                        "b1-01 = 1 hace que la referencia de frecuencia venga de la entrada " +
                        "analógica A1 en lugar del operador. En la velocidad 1 no hay ninguna " +
                        "entrada multi-velocidad activa, así que el variador va a buscar la " +
                        "referencia a A1... que no está cableada, y por tanto vale 0 V.\n" +
                        "Las velocidades 2 a 4 sí funcionan porque esas sí se toman de " +
                        "d1-02, d1-03 y d1-04. Un fallo parcial despista mucho más que uno " +
                        "total: invita a buscar el problema en la velocidad 1 y no en un " +
                        "parámetro que la afecta solo a ella.\n" +
                        "CÓMO REPARARLO: en el teclado del variador, b1-01 = 0: la referencia vuelve a salir del operador, de d1-01 a d1-04."),

                // --- Orden de marcha por el sitio equivocado --------------------------------
                VdfFallaDidactica.DeParametro(
                    "b1-02_OPERADOR",
                    "El PLC no manda nada sobre el motor",
                    "b1-02", 0f, 1f,
                    sintoma:
                        "El PLC funciona: se habilita, los indicadores responden y el " +
                        "diagrama Ladder muestra la marcha activa. Pero el motor sigue parado, y en " +
                        "cambio arranca si se pulsa RUN en el teclado del variador (a 10 Hz, la " +
                        "velocidad 1).",
                    explicacion:
                        "b1-02 = 0 pone la orden de marcha en el operador digital, no en los " +
                        "terminales. El variador está ignorando la marcha del PLC a propósito: solo escucha " +
                        "a su propia tecla RUN. El valor correcto es 1.\n" +
                        "La pista está en que el motor SÍ arranca desde el teclado: eso " +
                        "descarta el cableado de potencia y el motor, y señala a quién tiene " +
                        "el mando.\n" +
                        "CÓMO REPARARLO: en el teclado del variador, b1-02 = 1. Para cambiarlo, el motor tiene que estar parado."),

                // --- Cableado: el común que falta
                sinComun,

                // --- Cableado: un indicador suelto ------------------------------------------
                VdfFallaDidactica.DeCable(
                    "V3_SIN_CABLE",
                    "El indicador V3 no se enciende nunca",
                    "PLC-Q0.2", "V3-A",
                    sintoma:
                        "El motor funciona perfectamente en las cuatro velocidades, con sus " +
                        "frecuencias correctas. Pero al llegar a la velocidad 3 no se " +
                        "enciende ningún piloto.",
                    explicacion:
                        "Falta el cable entre la salida Q0.2 del PLC y el indicador V3. El " +
                        "PLC está activando la salida; lo que falta es el camino hasta la " +
                        "lámpara.\n" +
                        "Conviene fijarse en que el motor sí va a 35 Hz: el fallo está solo " +
                        "en la señalización, no en el control. Distinguir un fallo de mando " +
                        "de uno de señalización es parte de lo que la práctica enseña.\n" +
                        "CÓMO REPARARLO: vuelve a enchufar el cable suelto entre la salida Q0.2 del PLC y V3.")
            };
        }

        public static VdfFallaDidactica Elegir(string id = null)
        {
            if (!string.IsNullOrEmpty(id)) return Buscar(id);

            var todas = Crear();
            if (todas.Count == 0) return null;

            var quedan = new List<VdfFallaDidactica>();
            for (int i = 0; i < todas.Count; i++)
                if (!_salidas.Contains(todas[i].Id)) quedan.Add(todas[i]);
            if (quedan.Count == 0)
            {
                // Ronda nueva, pero sin repetir la última de la anterior.
                _salidas.Clear();
                quedan = new List<VdfFallaDidactica>();
                for (int i = 0; i < todas.Count; i++)
                    if (todas[i].Id != _ultima || todas.Count == 1) quedan.Add(todas[i]);
            }

            var elegida = quedan[UnityEngine.Random.Range(0, quedan.Count)];
            _salidas.Add(elegida.Id);
            _ultima = elegida.Id;
            return elegida;
        }

        /// <summary>Las que ya salieron en esta ronda de la baraja.</summary>
        private static readonly HashSet<string> _salidas = new HashSet<string>();
        private static string _ultima;

        /// <summary>Vuelve a barajar. Para los tests.</summary>
        public static void Barajar() { _salidas.Clear(); _ultima = null; }

        /// <summary>Busca una avería por su identificador.</summary>

        public static VdfFallaDidactica Buscar(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            var todas = Crear();
            for (int i = 0; i < todas.Count; i++)
                if (todas[i].Id == id) return todas[i];
            return null;
        }
    }
}
