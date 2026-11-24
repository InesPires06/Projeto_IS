var builder = WebApplication.CreateBuilder(args);

// Configura o servidor para escutar na porta 8765 (Secção 2.4 do Enunciado)
builder.WebHost.UseUrls("http://0.0.0.0:8765");

// Regista os serviços de Controllers e Swagger (Ficha 03)
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Ativa a interface Swagger em http://localhost:8765/swagger
app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();