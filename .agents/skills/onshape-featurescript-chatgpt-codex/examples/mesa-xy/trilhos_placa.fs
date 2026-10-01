FeatureScript 3083;
import(path : "onshape/std/geometry.fs", version : "3083.0");
import(path : "onshape/std/properties.fs", version : "3083.0");

// =====================================================================
// Trilhos da placa perfurada (placa de controle da mesa XY)
//
// Dois trilhos impressos, parafusados no MDF, seguram a placa perfurada pelas
// duas bordas laterais. A placa entra deslizando em +Y, para no batente do fim
// e fica presa por um dente em rampa num braco flexivel na entrada. Para soltar:
// apertar o dente para baixo e puxar a placa.
//
// Eixos (Z para cima): topo do MDF em Z = 0; placa em X [-L/2, L/2],
// Y [0, C] (entra pelo lado Y = 0); trilho direito em X > L/2, esquerdo e o
// espelho. Coordenada local "u" do trilho: 0 = borda nominal da placa,
// u > 0 para fora; a placa fica em u < 0.
//
// Impressao: em pe, com a ponta da entrada (Y minimo) na mesa e brim. Assim o
// braco, as fendas e a rampa saem sem suporte. Os dois trilhos sao espelhados
// (direito e esquerdo).
// =====================================================================

const MARGEM = 1;            // quanto a ferramenta de corte estoura a parede que atravessa
const LABIO = 2;             // espessura do labio sobre a placa
const BATENTE = 4;           // fundo fechado do trilho (Y alem da placa)
const ENTRADA = 4.5;         // trilho antes da placa, onde fica o dente
const FOLGA_LATERAL = 0.5;   // folga entre a borda da placa e o fundo da fenda
const BRACO_FIM = 12;        // raiz do braco flexivel (Y)
const BRACO_ESP = 1.6;       // espessura do braco (Z)
const VAO_BRACO = 1.5;       // vao sob o braco: quanto o dente pode descer
const VAO_LADO = 0.8;        // fenda que solta o braco do resto do trilho
const BRACO_RECUO = 0.3;     // braco termina antes do fundo da fenda: sem quina encostando so por uma aresta
const DENTE = 1.2;           // altura do dente acima do piso da fenda
const DENTE_RECUO = 0.3;     // folga entre a face reta do dente e a borda da placa
const FURO_PARAFUSO = 4;     // parafuso de madeira 3,5 mm
const CABECA = 7.2;          // rebaixo da cabeca
const PISO_CABECA = 2.5;     // plastico sob a cabeca
const PARAFUSO_PONTA = 20;   // distancia dos parafusos ate as pontas da placa (Y)

// -------------------- primitivas --------------------

// Bloco no referencial local do trilho: u (para fora da placa), y, z. "sx" = +1
// no trilho direito, -1 no esquerdo (espelho em X).
function blocoLocal(context is Context, id is Id, c is map, sx is number,
        u1 is number, u2 is number, y1 is number, y2 is number, z1 is number, z2 is number) returns Query
{
    var xa = sx * (c.meiaLargura + u1);
    var xb = sx * (c.meiaLargura + u2);
    fCuboid(context, id, {
            "corner1" : vector(min(xa, xb), min(y1, y2), min(z1, z2)) * millimeter,
            "corner2" : vector(max(xa, xb), max(y1, y2), max(z1, z2)) * millimeter
    });
    return qCreatedBy(id, EntityType.BODY);
}

// Cilindro vertical (Z) no ponto local (u, y).
function furoVertical(context is Context, id is Id, c is map, sx is number,
        u is number, y is number, z1 is number, z2 is number, diametro is number) returns Query
{
    var x = sx * (c.meiaLargura + u);
    fCylinder(context, id, {
            "bottomCenter" : vector(x, y, z1) * millimeter,
            "topCenter" : vector(x, y, z2) * millimeter,
            "radius" : diametro / 2 * millimeter
    });
    return qCreatedBy(id, EntityType.BODY);
}

// Une "additions" ao alvo e depois subtrai "cuts". Continue usando "target"
// como Query do corpo resultante (o Id do boolean nao e um corpo novo).
function combine(context is Context, id is Id, target is Query, additions is array, cuts is array)
{
    if (size(additions) > 0)
    {
        opBoolean(context, id + "join", {
                "tools" : qUnion(concatenateArrays([[target], additions])),
                "operationType" : BooleanOperationType.UNION
        });
    }
    if (size(cuts) > 0)
    {
        opBoolean(context, id + "cut", {
                "targets" : target,
                "tools" : qUnion(cuts),
                "operationType" : BooleanOperationType.SUBTRACTION
        });
    }
}

// Nome + cor de uma vez: todo corpo passa por aqui.
function nomear(context is Context, body is Query, label is string, cor is Color)
{
    setProperty(context, {
            "entities" : body,
            "propertyType" : PropertyType.NAME,
            "value" : label
    });
    setProperty(context, {
            "entities" : body,
            "propertyType" : PropertyType.APPEARANCE,
            "value" : cor
    });
}

// -------------------- cotas: FONTE UNICA --------------------

function cotas(d is map) returns map
{
    var placaLargura = d.placaLargura / millimeter;
    var placaComprimento = d.placaComprimento / millimeter;
    var placaEspessura = d.placaEspessura / millimeter;
    var encaixe = d.encaixe / millimeter;
    var larguraTrilho = d.larguraTrilho / millimeter;
    var alturaBase = d.alturaBase / millimeter;
    var fenda = placaEspessura + d.folgaFenda / millimeter;
    var uDentro = -encaixe;                      // face interna do trilho
    var uFora = larguraTrilho - encaixe;         // face externa do trilho
    return {
        "meiaLargura" : placaLargura / 2,
        "placaComprimento" : placaComprimento,
        "placaEspessura" : placaEspessura,
        "alturaBase" : alturaBase,
        "fenda" : fenda,
        "topo" : alturaBase + fenda + LABIO,
        "uDentro" : uDentro,
        "uFora" : uFora,
        "uParafuso" : (FOLGA_LATERAL + uFora) / 2,   // meio da parte macica, fora da fenda
        "uBraco" : FOLGA_LATERAL - BRACO_RECUO,      // face externa do braco e do dente
        "pisoBraco" : alturaBase - BRACO_ESP - VAO_BRACO
    };
}

// -------------------- pecas --------------------

function trilho(context is Context, id is Id, c is map, sx is number, label is string)
{
    var h = c.alturaBase;
    var yFim = c.placaComprimento + BATENTE;

    var corpo = blocoLocal(context, id + "corpo", c, sx, c.uDentro, c.uFora, -ENTRADA, yFim, 0, c.topo);

    var cortes = [];
    // fenda da placa: aberta na entrada, fechada no batente (para em Y = C)
    cortes = append(cortes, blocoLocal(context, id + "fenda", c, sx,
                c.uDentro - MARGEM, FOLGA_LATERAL, -ENTRADA - MARGEM, c.placaComprimento, h, h + c.fenda));
    // labio aberto sobre o dente, para apertar o dente com o dedo
    cortes = append(cortes, blocoLocal(context, id + "janelaDente", c, sx,
                c.uDentro - MARGEM, FOLGA_LATERAL, -ENTRADA - MARGEM, 0, h + c.fenda - MARGEM, c.topo + MARGEM));
    // vao sob o braco e fenda lateral que soltam o braco (raiz em Y = BRACO_FIM)
    cortes = append(cortes, blocoLocal(context, id + "vaoBraco", c, sx,
                c.uDentro - MARGEM, FOLGA_LATERAL + VAO_LADO, -ENTRADA - MARGEM, BRACO_FIM, c.pisoBraco, h - BRACO_ESP));
    cortes = append(cortes, blocoLocal(context, id + "vaoLado", c, sx,
                c.uBraco, FOLGA_LATERAL + VAO_LADO, -ENTRADA - MARGEM, BRACO_FIM, c.pisoBraco, h));
    // dois parafusos de madeira com a cabeca rebaixada
    var i = 0;
    for (var y in [PARAFUSO_PONTA, c.placaComprimento - PARAFUSO_PONTA])
    {
        cortes = append(cortes, furoVertical(context, id + ("furo" ~ i), c, sx, c.uParafuso, y,
                    -MARGEM, c.topo + MARGEM, FURO_PARAFUSO));
        cortes = append(cortes, furoVertical(context, id + ("cabeca" ~ i), c, sx, c.uParafuso, y,
                    PISO_CABECA, c.topo + MARGEM, CABECA));
        i += 1;
    }
    combine(context, id + "boolCorpo", corpo, [], cortes);

    // dente em rampa na ponta do braco: bloco cortado por um plano inclinado que
    // sobe da entrada ate a face reta (Y = -DENTE_RECUO), atras da borda da placa
    var dente = blocoLocal(context, id + "dente", c, sx,
            c.uDentro, c.uBraco, -ENTRADA, -DENTE_RECUO, h - 0.5, h + DENTE);
    var cunha = blocoLocal(context, id + "cunha", c, sx,
            c.uDentro - MARGEM, FOLGA_LATERAL + MARGEM, -ENTRADA, -ENTRADA + 20, h, h + 10);
    opTransform(context, id + "girarCunha", {
            "bodies" : cunha,
            "transform" : rotationAround(line(vector(0, -ENTRADA, h) * millimeter, vector(1, 0, 0)),
                    atan(DENTE / (ENTRADA - DENTE_RECUO)))
    });
    combine(context, id + "boolDente", dente, [], [cunha]);

    // o dente mergulha 0,5 mm (em Z) no braco
    combine(context, id + "boolUniao", corpo, [dente], []);
    nomear(context, corpo, label, color(1.0, 0.45, 0.30));
}

function placaReferencia(context is Context, id is Id, c is map)
{
    fCuboid(context, id + "placa", {
            "corner1" : vector(-c.meiaLargura, 0, c.alturaBase) * millimeter,
            "corner2" : vector(c.meiaLargura, c.placaComprimento, c.alturaBase + c.placaEspessura) * millimeter
    });
    nomear(context, qCreatedBy(id + "placa", EntityType.BODY), "Placa perfurada (referencia)", color(0.55, 0.75, 0.35));
}

// -------------------- feature --------------------

annotation { "Feature Type Name" : "Trilhos da placa perfurada" }
export const trilhosPlaca = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {
        annotation { "Name" : "Placa - largura (entre trilhos)" }
        isLength(definition.placaLargura, { (millimeter) : [50, 100, 200] } as LengthBoundSpec);

        annotation { "Name" : "Placa - comprimento (sentido de entrada)" }
        isLength(definition.placaComprimento, { (millimeter) : [50, 100, 200] } as LengthBoundSpec);

        annotation { "Name" : "Placa - espessura" }
        isLength(definition.placaEspessura, { (millimeter) : [0.8, 1.5, 3] } as LengthBoundSpec);

        annotation { "Name" : "Folga da fenda (altura)" }
        isLength(definition.folgaFenda, { (millimeter) : [0.1, 0.3, 1] } as LengthBoundSpec);

        annotation { "Name" : "Quanto a placa entra no trilho" }
        isLength(definition.encaixe, { (millimeter) : [1.5, 3, 6] } as LengthBoundSpec);

        annotation { "Name" : "Largura do trilho" }
        isLength(definition.larguraTrilho, { (millimeter) : [10, 14, 25] } as LengthBoundSpec);

        annotation { "Name" : "Altura livre sob a placa" }
        isLength(definition.alturaBase, { (millimeter) : [3, 5, 15] } as LengthBoundSpec);

        annotation { "Name" : "Mostrar a placa", "Default" : true }
        definition.mostrarPlaca is boolean;
    }
    {
        var c = cotas(definition);

        if (c.pisoBraco < 1.5)
        {
            throw regenError("Sem piso sob o braco da trava: aumente Altura livre sob a placa (minimo 4,6 mm).");
        }
        if (DENTE > c.fenda - 0.3)
        {
            throw regenError("Dente da trava nao cabe na fenda: aumente Folga da fenda ou Placa - espessura.");
        }
        if (c.uFora - CABECA / 2 - c.uParafuso < 1 || c.uParafuso - CABECA / 2 - FOLGA_LATERAL < 1)
        {
            throw regenError("Rebaixo do parafuso nao cabe no trilho: aumente Largura do trilho.");
        }
        if (c.placaComprimento - PARAFUSO_PONTA <= PARAFUSO_PONTA + CABECA)
        {
            throw regenError("Placa curta demais para os dois parafusos: aumente Placa - comprimento.");
        }

        trilho(context, id + "dir", c, 1, "Trilho direito");
        trilho(context, id + "esq", c, -1, "Trilho esquerdo");
        if (definition.mostrarPlaca)
        {
            placaReferencia(context, id, c);
        }
    });
