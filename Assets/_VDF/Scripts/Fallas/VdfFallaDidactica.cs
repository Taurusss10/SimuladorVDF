namespace VDF
{
    /// <summary>Qué clase de avería se provoca.</summary>
    public enum VdfTipoFalla
    {
        /// <summary>Un parámetro del variador con un valor incorrecto.</summary>
        ParametroMal,
        /// <summary>Dos parámetros intercambiados entre sí.</summary>
        ParametrosIntercambiados,
        /// <summary>Falta un cable: dos bornes que deberían estar unidos no lo están.</summary>
        CableFaltante
    }

    /// <summary>
    /// Una avería que el simulador provoca a propósito para que el alumno la diagnostique.
    ///
    /// Lo que distingue a estas averías de un simple «valor incorrecto» es que **el síntoma
    /// no apunta a la causa**. Con H1-01 = 1 el motor no arranca y todo lo demás parece
    /// correcto; con H1-05 y H1-06 cruzados el motor arranca y las cuatro velocidades
    /// funcionan, pero dos de ellas salen cambiadas de orden. Enseñar a leer ese salto entre
    /// lo que se ve y lo que pasa es el objetivo de la práctica 5.
    ///
    /// Ninguna avería está programada como un caso especial: todas se provocan escribiendo en
    /// el modelo o en el grafo **lo mismo que escribiría un alumno equivocándose**, y el
    /// comportamiento que resulta emerge solo. Por eso el síntoma es real y no un texto.
    /// </summary>
    public sealed class VdfFallaDidactica
    {
        public readonly string Id;
        public readonly string Titulo;

        /// <summary>Qué observa el alumno. Es la pista con la que tiene que trabajar.</summary>
        public readonly string Sintoma;

        /// <summary>Por qué pasa. Se revela al diagnosticarla, no antes.</summary>
        public readonly string Explicacion;

        public readonly VdfTipoFalla Tipo;

        /// <summary>Parámetro o borne afectado.</summary>
        public readonly string ObjetivoA;

        /// <summary>Segundo parámetro o borne. Vacío si la avería solo toca uno.</summary>
        public readonly string ObjetivoB;

        /// <summary>Valor correcto de <see cref="ObjetivoA"/>. Es el que la repara.</summary>
        public readonly float ValorCorrectoA;

        /// <summary>Valor correcto de <see cref="ObjetivoB"/>, cuando la avería toca dos.</summary>
        public readonly float ValorCorrectoB;

        /// <summary>Valor equivocado que se escribe al provocarla. Solo en ParametroMal.</summary>
        public readonly float ValorIncorrecto;

        private VdfFallaDidactica(string id, string titulo, VdfTipoFalla tipo,
                                  string objetivoA, string objetivoB,
                                  float correctoA, float correctoB, float incorrecto,
                                  string sintoma, string explicacion)
        {
            Id = id;
            Titulo = titulo;
            Tipo = tipo;
            ObjetivoA = objetivoA;
            ObjetivoB = objetivoB;
            ValorCorrectoA = correctoA;
            ValorCorrectoB = correctoB;
            ValorIncorrecto = incorrecto;
            Sintoma = sintoma;
            Explicacion = explicacion;
        }

        // --- Constructores con nombre, para que al leer el catálogo se entienda qué es cada cosa

        public static VdfFallaDidactica DeParametro(string id, string titulo, string parametro,
                                                    float valorIncorrecto, float valorCorrecto,
                                                    string sintoma, string explicacion)
        {
            return new VdfFallaDidactica(id, titulo, VdfTipoFalla.ParametroMal,
                                         parametro, null,
                                         valorCorrecto, 0f, valorIncorrecto,
                                         sintoma, explicacion);
        }

        public static VdfFallaDidactica DeIntercambio(string id, string titulo,
                                                      string parametroA, float correctoA,
                                                      string parametroB, float correctoB,
                                                      string sintoma, string explicacion)
        {
            return new VdfFallaDidactica(id, titulo, VdfTipoFalla.ParametrosIntercambiados,
                                         parametroA, parametroB,
                                         correctoA, correctoB, 0f,
                                         sintoma, explicacion);
        }

        public static VdfFallaDidactica DeCable(string id, string titulo,
                                                string borneA, string borneB,
                                                string sintoma, string explicacion)
        {
            return new VdfFallaDidactica(id, titulo, VdfTipoFalla.CableFaltante,
                                         borneA, borneB, 0f, 0f, 0f,
                                         sintoma, explicacion);
        }

        // =====================================================================================

        /// <summary>Provoca la avería. Devuelve false si no se pudo.</summary>
        public bool Aplicar(VfdModel vfd, VdfWiringGraph grafo)
        {
            switch (Tipo)
            {
                case VdfTipoFalla.ParametroMal:
                    if (vfd == null) return false;
                    return vfd.EscribirParametro(ObjetivoA, ValorIncorrecto);

                case VdfTipoFalla.ParametrosIntercambiados:
                    if (vfd == null) return false;
                    // Se cruzan de verdad, para que el alumno vea el efecto real de tenerlos
                    // al revés y no una simulación del efecto.
                    return vfd.EscribirParametro(ObjetivoA, ValorCorrectoB) &&
                           vfd.EscribirParametro(ObjetivoB, ValorCorrectoA);

                case VdfTipoFalla.CableFaltante:
                    if (grafo == null) return false;
                    grafo.Desconectar(ObjetivoA, ObjetivoB);
                    return !grafo.MismaRed(ObjetivoA, ObjetivoB);
            }
            return false;
        }

        /// <summary>¿Sigue presente la avería?</summary>
        public bool EstaPresente(VfdModel vfd, VdfWiringGraph grafo)
        {
            switch (Tipo)
            {
                case VdfTipoFalla.ParametroMal:
                    if (vfd == null) return false;
                    return !CasiIgual(vfd.Valor(ObjetivoA), ValorCorrectoA);

                case VdfTipoFalla.ParametrosIntercambiados:
                    if (vfd == null) return false;
                    // Hace falta que CADA UNO vuelva a su valor. Dejar los dos en 3 también
                    // sería incorrecto, y con una comprobación de «son distintos» colaría.
                    return !CasiIgual(vfd.Valor(ObjetivoA), ValorCorrectoA) ||
                           !CasiIgual(vfd.Valor(ObjetivoB), ValorCorrectoB);

                case VdfTipoFalla.CableFaltante:
                    if (grafo == null) return false;
                    return !grafo.MismaRed(ObjetivoA, ObjetivoB);
            }
            return false;
        }

        public bool Reparada(VfdModel vfd, VdfWiringGraph grafo)
        {
            return !EstaPresente(vfd, grafo);
        }

        private static bool CasiIgual(float x, float y)
        {
            return UnityEngine.Mathf.Abs(x - y) <= 0.005f;
        }

        public override string ToString()
        {
            return Id + " (" + Titulo + ")";
        }
    }
}
