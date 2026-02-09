using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using PagueVeloz.Application.DTOs;
using PagueVeloz.IntegrationTests.Fixtures;
using Xunit;

namespace PagueVeloz.IntegrationTests.Scenarios;

public sealed class TransactionEndpointTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task CreateAccount_ShouldReturn201()
    {
        var response = await PostJsonAsync("/api/accounts", new
        {
            client_id = "CLI-001",
            account_id = "ACC-001",
            initial_balance = 0,
            credit_limit = 50000,
            currency = "BRL"
        });

        var responseBody = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Created, responseBody);
        var body = await DeserializeAsync<AccountResponse>(response);
        body.Should().NotBeNull();
        body!.AccountId.Should().Be("ACC-001");
    }

    [Fact]
    public async Task CreditAndDebit_ShouldProcessCorrectly()
    {
        await CreateTestAccount("ACC-100");

        var creditResponse = await PostJsonAsync("/api/transactions", new
        {
            operation = "credit",
            account_id = "ACC-100",
            amount = 100000,
            currency = "BRL",
            reference_id = "TXN-100"
        });

        creditResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var creditResult = await DeserializeAsync<TransactionResponse>(creditResponse);
        creditResult!.Status.Should().Be("success");
        creditResult.Balance.Should().Be(100000);

        var debitResponse = await PostJsonAsync("/api/transactions", new
        {
            operation = "debit",
            account_id = "ACC-100",
            amount = 20000,
            currency = "BRL",
            reference_id = "TXN-101"
        });

        debitResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var debitResult = await DeserializeAsync<TransactionResponse>(debitResponse);
        debitResult!.Status.Should().Be("success");
        debitResult.Balance.Should().Be(80000);
    }

    [Fact]
    public async Task InsufficientFunds_ShouldReturn422()
    {
        await CreateTestAccount("ACC-200");

        await PostJsonAsync("/api/transactions", new
        {
            operation = "credit",
            account_id = "ACC-200",
            amount = 10000,
            currency = "BRL",
            reference_id = "TXN-200"
        });

        var response = await PostJsonAsync("/api/transactions", new
        {
            operation = "debit",
            account_id = "ACC-200",
            amount = 50000,
            currency = "BRL",
            reference_id = "TXN-201"
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task BatchTransactions_ShouldProcessAll()
    {
        await CreateTestAccount("ACC-300");

        var batch = new[]
        {
            new { operation = "credit", account_id = "ACC-300", amount = 100000, currency = "BRL", reference_id = "TXN-300" },
            new { operation = "debit", account_id = "ACC-300", amount = 20000, currency = "BRL", reference_id = "TXN-301" },
            new { operation = "debit", account_id = "ACC-300", amount = 90000, currency = "BRL", reference_id = "TXN-302" }
        };

        var response = await PostJsonAsync("/api/transactions/batch", batch);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var results = await DeserializeAsync<List<TransactionResponse>>(response);
        results.Should().HaveCount(3);
        results![0].Status.Should().Be("success");
        results[1].Status.Should().Be("success");
        results[2].Status.Should().Be("failed");
    }

    [Fact]
    public async Task Idempotency_DuplicateReferenceId_ShouldReturnSameResult()
    {
        await CreateTestAccount("ACC-400");

        var payload = new
        {
            operation = "credit",
            account_id = "ACC-400",
            amount = 10000,
            currency = "BRL",
            reference_id = "TXN-400"
        };

        var response1 = await PostJsonAsync("/api/transactions", payload);
        var result1 = await DeserializeAsync<TransactionResponse>(response1);

        var response2 = await PostJsonAsync("/api/transactions", payload);
        var result2 = await DeserializeAsync<TransactionResponse>(response2);

        result1!.TransactionId.Should().Be(result2!.TransactionId);
        result1.Balance.Should().Be(result2.Balance);
    }

    [Fact]
    public async Task ReserveAndCapture_ShouldProcessCorrectly()
    {
        await CreateTestAccount("ACC-500");

        await PostJsonAsync("/api/transactions", new
        {
            operation = "credit",
            account_id = "ACC-500",
            amount = 100000,
            currency = "BRL",
            reference_id = "TXN-500"
        });

        var reserveResponse = await PostJsonAsync("/api/transactions", new
        {
            operation = "reserve",
            account_id = "ACC-500",
            amount = 40000,
            currency = "BRL",
            reference_id = "TXN-501"
        });
        reserveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var reserveResult = await DeserializeAsync<TransactionResponse>(reserveResponse);
        reserveResult!.Status.Should().Be("success");
        reserveResult.ReservedBalance.Should().Be(40000);
        reserveResult.AvailableBalance.Should().Be(60000);

        var captureResponse = await PostJsonAsync("/api/transactions", new
        {
            operation = "capture",
            account_id = "ACC-500",
            amount = 40000,
            currency = "BRL",
            reference_id = "TXN-502"
        });
        captureResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var captureResult = await DeserializeAsync<TransactionResponse>(captureResponse);
        captureResult!.Status.Should().Be("success");
        captureResult.Balance.Should().Be(60000);
        captureResult.ReservedBalance.Should().Be(0);
    }

    [Fact]
    public async Task Reversal_CreditOperation_ShouldReverseSuccessfully()
    {
        await CreateTestAccount("ACC-600");

        await PostJsonAsync("/api/transactions", new
        {
            operation = "credit",
            account_id = "ACC-600",
            amount = 50000,
            currency = "BRL",
            reference_id = "TXN-600"
        });

        var reversalResponse = await PostJsonAsync("/api/transactions", new
        {
            operation = "reversal",
            account_id = "ACC-600",
            amount = 50000,
            currency = "BRL",
            reference_id = "TXN-601",
            metadata = new Dictionary<string, string> { { "original_reference_id", "TXN-600" } }
        });
        reversalResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var reversalResult = await DeserializeAsync<TransactionResponse>(reversalResponse);
        reversalResult!.Status.Should().Be("success");
        reversalResult.Balance.Should().Be(0);
    }

    [Fact]
    public async Task Reversal_DebitOperation_ShouldReverseSuccessfully()
    {
        await CreateTestAccount("ACC-610");

        await PostJsonAsync("/api/transactions", new
        {
            operation = "credit",
            account_id = "ACC-610",
            amount = 100000,
            currency = "BRL",
            reference_id = "TXN-610"
        });

        await PostJsonAsync("/api/transactions", new
        {
            operation = "debit",
            account_id = "ACC-610",
            amount = 30000,
            currency = "BRL",
            reference_id = "TXN-611"
        });

        var reversalResponse = await PostJsonAsync("/api/transactions", new
        {
            operation = "reversal",
            account_id = "ACC-610",
            amount = 30000,
            currency = "BRL",
            reference_id = "TXN-612",
            metadata = new Dictionary<string, string> { { "original_reference_id", "TXN-611" } }
        });
        reversalResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var reversalResult = await DeserializeAsync<TransactionResponse>(reversalResponse);
        reversalResult!.Status.Should().Be("success");
        reversalResult.Balance.Should().Be(100000);
    }

    [Fact]
    public async Task CreditLimit_ShouldAllowOverdraft()
    {
        await PostJsonAsync("/api/accounts", new
        {
            client_id = "CLI-CL",
            account_id = "ACC-700",
            initial_balance = 0,
            credit_limit = 50000,
            currency = "BRL"
        });

        await PostJsonAsync("/api/transactions", new
        {
            operation = "credit",
            account_id = "ACC-700",
            amount = 30000,
            currency = "BRL",
            reference_id = "TXN-700"
        });

        var debitResponse = await PostJsonAsync("/api/transactions", new
        {
            operation = "debit",
            account_id = "ACC-700",
            amount = 60000,
            currency = "BRL",
            reference_id = "TXN-701"
        });
        debitResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var debitResult = await DeserializeAsync<TransactionResponse>(debitResponse);
        debitResult!.Status.Should().Be("success");
        debitResult.Balance.Should().Be(-30000);
    }

    [Fact]
    public async Task CreditLimit_ShouldRejectWhenExceeded()
    {
        await PostJsonAsync("/api/accounts", new
        {
            client_id = "CLI-CL2",
            account_id = "ACC-710",
            initial_balance = 0,
            credit_limit = 50000,
            currency = "BRL"
        });

        await PostJsonAsync("/api/transactions", new
        {
            operation = "credit",
            account_id = "ACC-710",
            amount = 30000,
            currency = "BRL",
            reference_id = "TXN-710"
        });

        var debitResponse = await PostJsonAsync("/api/transactions", new
        {
            operation = "debit",
            account_id = "ACC-710",
            amount = 90000,
            currency = "BRL",
            reference_id = "TXN-711"
        });
        debitResponse.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Transfer_ShouldMoveBalanceBetweenAccounts()
    {
        await CreateTestAccount("ACC-800");
        await CreateTestAccount("ACC-801");

        await PostJsonAsync("/api/transactions", new
        {
            operation = "credit",
            account_id = "ACC-800",
            amount = 100000,
            currency = "BRL",
            reference_id = "TXN-800"
        });

        var transferResponse = await PostJsonAsync("/api/transactions", new
        {
            operation = "transfer",
            account_id = "ACC-800",
            destination_account_id = "ACC-801",
            amount = 40000,
            currency = "BRL",
            reference_id = "TXN-801"
        });
        transferResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var transferResult = await DeserializeAsync<TransactionResponse>(transferResponse);
        transferResult!.Status.Should().Be("success");
        transferResult.Balance.Should().Be(60000);
        transferResult.CreditTransactionId.Should().NotBeNullOrWhiteSpace();

        var destBalanceResponse = await _client.GetAsync("/api/accounts/ACC-801/balance");
        destBalanceResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var destBalance = await DeserializeAsync<AccountResponse>(destBalanceResponse);
        destBalance!.Balance.Should().Be(40000);
    }

    private async Task CreateTestAccount(string accountId)
    {
        await PostJsonAsync("/api/accounts", new
        {
            client_id = "CLI-TEST",
            account_id = accountId,
            initial_balance = 0,
            credit_limit = 0,
            currency = "BRL"
        });
    }

    private async Task<HttpResponseMessage> PostJsonAsync<T>(string url, T payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await _client.PostAsync(url, content);
    }

    private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }
}
