using System;
using System.Collections.Generic;
using System.Net;
using System.Web.Mvc;
using UnaccenType.Models;

namespace UnaccenType.Controllers
{
    public class HomeController : Controller
    {
        public static readonly Language DefaultLanguage = Language.Vietnamese;
        public static readonly string Version = "Demo";

        private Predictor m_Predictor = Predictor.SingletonInstance;
        public ActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Predict(string language, string query)
        {
            if (String.IsNullOrWhiteSpace(language) || String.IsNullOrWhiteSpace(query))
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            Language lang = (Language)Enum.Parse(typeof(Language), language);

            string[] segments = query.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);

            List<string[][]> segmentChoices = new List<string[][]>();

            foreach (string segment in segments)
            {
                string[][] wordChoices = m_Predictor.Predict(lang, segment);
                if (wordChoices != null)
                {
                    segmentChoices.Add(wordChoices);
                }
            }

            return Json(segmentChoices);
        }
    }
}