using MailKit.Net.Smtp;
using Microsoft.AspNetCore.WebUtilities;
using MimeKit;
using System.Text;
using UniversityTinder.Services.IServices;

namespace UniversityTinder.Services
{
    public class EmailService : IEmailService
    {
        private readonly ILogger<EmailService> _logger;

        public EmailService(ILogger<EmailService> logger)
        {
            _logger = logger;
        }

        public async Task SendPasswordResetCodeAsync(string email, string resetCode)
        {
            try
            {
                var subject = "Şifre Sıfırlama Kodu - Pafic App";

                var htmlMessage = $@"
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset='utf-8'>
            <style>
                body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                .header {{ background-color: #4CAF50; color: white; padding: 20px; text-align: center; }}
                .content {{ padding: 20px; background-color: #f9f9f9; }}
                .code-box {{ 
                    background-color: #e8f5e8; 
                    padding: 20px; 
                    border-radius: 10px; 
                    margin: 20px 0;
                    text-align: center;
                    border: 3px solid #4CAF50;
                }}
                .code {{ 
                    font-size: 32px; 
                    font-weight: bold; 
                    color: #2E7D32; 
                    letter-spacing: 8px;
                    font-family: monospace;
                }}
                .footer {{ text-align: center; padding: 20px; font-size: 12px; color: #666; }}
                .warning {{ background-color: #fff3cd; padding: 10px; border-radius: 5px; margin: 15px 0; }}
            </style>
        </head>
        <body>
            <div class='container'>
                <div class='header'>
                    <h1>🔐 Şifre Sıfırlama Kodu</h1>
                </div>
                <div class='content'>
                    <h2>Merhaba,</h2>
                    <p>Pafic App hesabınız için şifre sıfırlama talebinde bulundunuz.</p>
                    <p>Şifrenizi sıfırlamak için aşağıdaki 6 haneli kodu kullanın:</p>
                    
                    <div class='code-box'>
                        <div class='code'>{resetCode}</div>
                    </div>
                    
                    <div class='warning'>
                        <strong>⚠️ Önemli Bilgiler:</strong>
                        <ul style='text-align: left; margin: 10px 0;'>
                            <li>Bu kod sadece <strong>15 dakika</strong> geçerlidir</li>
                            <li>Kod sadece <strong>bir kez</strong> kullanılabilir</li>
                            <li>Kodu kimseyle paylaşmayın</li>
                        </ul>
                    </div>
                    
                    <p><strong>Nasıl kullanılır:</strong></p>
                    <ol style='text-align: left;'>
                        <li>Mobil uygulamada 'Şifremi Unuttum' sayfasına gidin</li>
                        <li>E-mail adresinizi girin</li>
                        <li>Bu 6 haneli kodu girin: <strong>{resetCode}</strong></li>
                        <li>Yeni şifrenizi belirleyin</li>
                    </ol>
                    
                    <p>Eğer bu talebi siz yapmadıysanız, bu e-postayı görmezden gelebilirsiniz.</p>
                </div>
                <div class='footer'>
                    <p>Bu otomatik bir e-postadır, lütfen yanıtlamayın.</p>
                    <p>© 2025 Pafic App. Tüm hakları saklıdır.</p>
                    <p style='color: #999;'>Kod 15 dakika sonra otomatik olarak geçersiz olacaktır.</p>
                </div>
            </div>
        </body>
        </html>";

                await SendEmailAsync(email, subject, htmlMessage);
                _logger.LogInformation("Şifre sıfırlama kodu e-postası gönderildi: {Email}", email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Şifre sıfırlama kodu e-postası gönderilirken hata oluştu: {Email}", email);
                throw;
            }
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            var emailMessage = new MimeMessage();
            emailMessage.From.Add(new MailboxAddress("Pafic App", "keremyilmazeng@gmail.com"));
            emailMessage.To.Add(new MailboxAddress("", email));
            emailMessage.Subject = subject;

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = htmlMessage
            };

            emailMessage.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            try
            {
                string appPassword = "axbl tjfz omzf afks";

                await client.ConnectAsync("smtp.gmail.com", 587, MailKit.Security.SecureSocketOptions.StartTls);
                await client.AuthenticateAsync("keremyilmazeng@gmail.com", appPassword);
                await client.SendAsync(emailMessage);

                _logger.LogInformation("E-posta başarıyla gönderildi: {Email}", email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "E-posta gönderilirken hata oluştu: {Email}", email);
                throw;
            }
            finally
            {
                await client.DisconnectAsync(true);
            }
        }

        public async Task SendVerificationEmailAsync(string email, string token, string userId)
        {
            try
            {
                var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
                var verificationLink = $"https://universitytinder.justkey.online/api/user/verify-email?userId={userId}&token={encodedToken}";

                var subject = "Email Doğrulama - UniversityTinder";

                var htmlMessage = $@"
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <meta charset='utf-8'>
                        <style>
                            body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                            .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                            .header {{ background-color: #E91E63; color: white; padding: 20px; text-align: center; border-radius: 10px 10px 0 0; }}
                            .content {{ padding: 30px; background-color: #f9f9f9; }}
                            .btn {{ 
                                display: inline-block;
                                background-color: #E91E63; 
                                color: white !important; 
                                padding: 15px 40px; 
                                text-decoration: none; 
                                border-radius: 25px;
                                font-weight: bold;
                                margin: 20px 0;
                            }}
                            .btn:hover {{ background-color: #C2185B; }}
                            .footer {{ text-align: center; padding: 20px; font-size: 12px; color: #666; }}
                            .warning {{ background-color: #fff3cd; padding: 15px; border-radius: 5px; margin: 15px 0; }}
                            .link-box {{ 
                                background-color: #e8e8e8; 
                                padding: 10px; 
                                border-radius: 5px; 
                                word-break: break-all;
                                font-size: 12px;
                                margin-top: 20px;
                            }}
                        </style>
                    </head>
                    <body>
                        <div class='container'>
                            <div class='header'>
                                <h1>💕 UniversityTinder</h1>
                                <p>Email Doğrulama</p>
                            </div>
                            <div class='content'>
                                <h2>Hoş Geldin! 🎉</h2>
                                <p>UniversityTinder'a kayıt olduğun için teşekkürler!</p>
                                <p>Hesabını aktif etmek ve üniversite öğrencisi olduğunu doğrulamak için aşağıdaki butona tıkla:</p>
            
                                <div style='text-align: center;'>
                                    <a href='{verificationLink}' class='btn'>✉️ Email Adresimi Doğrula</a>
                                </div>
            
                                <div class='warning'>
                                    <strong>⏰ Önemli:</strong>
                                    <ul style='margin: 10px 0;'>
                                        <li>Bu link <strong>24 saat</strong> geçerlidir</li>
                                        <li>Link sadece <strong>bir kez</strong> kullanılabilir</li>
                                        <li>Doğrulama yapılmadan eşleşme özelliklerini kullanamazsın</li>
                                    </ul>
                                </div>
            
                                <p>Buton çalışmıyorsa, aşağıdaki linki tarayıcına kopyala:</p>
                                <div class='link-box'>
                                    {verificationLink}
                                </div>
            
                                <p style='margin-top: 20px; color: #666;'>
                                    Eğer bu hesabı sen oluşturmadıysan, bu emaili görmezden gelebilirsin.
                                </p>
                            </div>
                            <div class='footer'>
                                <p>Bu otomatik bir e-postadır, lütfen yanıtlamayın.</p>
                                <p>© 2025 UniversityTinder. Tüm hakları saklıdır.</p>
                            </div>
                        </div>
                    </body>
                    </html>";

                await SendEmailAsync(email, subject, htmlMessage);
                _logger.LogInformation("Email doğrulama maili gönderildi: {Email}", email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email doğrulama maili gönderilemedi: {Email}", email);
                throw;
            }
        }

        public async Task SendEmailVerificationCodeAsync(string email, string verificationCode, string userName)
        {
            try
            {
                var subject = "Email Doğrulama Kodu - UniversityTinder";

                var htmlMessage = $@"
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset='utf-8'>
            <style>
                body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                .header {{ background-color: #E91E63; color: white; padding: 20px; text-align: center; }}
                .content {{ padding: 20px; background-color: #f9f9f9; }}
                .code-box {{ 
                    background-color: #fce4ec; 
                    padding: 20px; 
                    border-radius: 10px; 
                    margin: 20px 0;
                    text-align: center;
                    border: 3px solid #E91E63;
                }}
                .code {{ 
                    font-size: 32px; 
                    font-weight: bold; 
                    color: #C2185B; 
                    letter-spacing: 8px;
                    font-family: monospace;
                }}
                .footer {{ text-align: center; padding: 20px; font-size: 12px; color: #666; }}
                .warning {{ background-color: #fff3cd; padding: 10px; border-radius: 5px; margin: 15px 0; }}
            </style>
        </head>
        <body>
            <div class='container'>
                <div class='header'>
                    <h1>💕 UniversityTinder</h1>
                    <h2>Email Doğrulama</h2>
                </div>
                <div class='content'>
                    <h2>Hoş Geldin {userName}! 🎉</h2>
                    <p>UniversityTinder'a kayıt olduğun için teşekkürler!</p>
                    <p>Hesabını aktif etmek için aşağıdaki 6 haneli doğrulama kodunu kullan:</p>
                    
                    <div class='code-box'>
                        <div class='code'>{verificationCode}</div>
                    </div>
                    
                    <div class='warning'>
                        <strong>⚠️ Önemli Bilgiler:</strong>
                        <ul style='text-align: left; margin: 10px 0;'>
                            <li>Bu kod sadece <strong>15 dakika</strong> geçerlidir</li>
                            <li>Kod sadece <strong>bir kez</strong> kullanılabilir</li>
                            <li>Kodu kimseyle paylaşmayın</li>
                        </ul>
                    </div>
                    
                    <p><strong>Nasıl kullanılır:</strong></p>
                    <ol style='text-align: left;'>
                        <li>Mobil uygulamada 'Email Doğrulama' sayfasına git</li>
                        <li>Bu 6 haneli kodu gir: <strong>{verificationCode}</strong></li>
                        <li>Hesabın aktif olacak ve eşleşme özelliklerini kullanabileceksin!</li>
                    </ol>
                    
                    <p>Eğer bu hesabı sen oluşturmadıysan, bu e-postayı görmezden gelebilirsin.</p>
                </div>
                <div class='footer'>
                    <p>Bu otomatik bir e-postadır, lütfen yanıtlamayın.</p>
                    <p>© 2025 UniversityTinder. Tüm hakları saklıdır.</p>
                    <p style='color: #999;'>Kod 15 dakika sonra otomatik olarak geçersiz olacaktır.</p>
                </div>
            </div>
        </body>
        </html>";

                await SendEmailAsync(email, subject, htmlMessage);
                _logger.LogInformation("Email doğrulama kodu gönderildi: {Email}", email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email doğrulama kodu gönderilemedi: {Email}", email);
                throw;
            }
        }
    }
}
