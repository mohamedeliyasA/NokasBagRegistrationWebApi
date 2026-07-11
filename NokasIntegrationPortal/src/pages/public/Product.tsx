import PublicLayout from "../../layouts/PublicLayout";

export default function Product() {
  return (
    <PublicLayout>
      <h1 className="text-4xl font-bold mb-12 text-[#014737] text-center">Our Products & Services</h1>

      <div className="space-y-16">
        <Category letter="A" items={["autoCash Bank", "autoCash Drop", "autoCash Multi", "autoCash Retail", "Vending machine service"]} />
        <Category letter="B" items={["Backup tape", "Bank-neutral ATMs"]} />
        <Category letter="C" items={["coreCash for banking"]} />
        <Category letter="M" items={["ATM in store", "ATM outsourcing"]} />
        <Category letter="N" items={["Night safe settlement"]} />
        <Category letter="O" items={["Storage of valuables"]} />
        <Category letter="S" items={["ATM service"]} />
        <Category letter="V" items={["Currency", "Currency services to banks", "Bill of exchange", "Currency exchange safe", "Exchanging expired banknotes", "Cash on delivery"]} />
      </div>

      <div className="mt-20 bg-green-50 border border-green-300 p-8 rounded shadow-lg max-w-6xl mx-auto">
        <h2 className="text-2xl font-semibold mb-3 text-[#014737]">Questions about products?</h2>
        <p className="mb-4 text-gray-700">
          Do you have questions about our products and services? If you would like to order or need to know more –
          fill in your contact information and we will contact you as soon as possible.
        </p>
        <a
          href="/contact"
          className="inline-block mt-4 bg-[#014737] text-white px-6 py-3 rounded hover:bg-[#02614f] transition"
        >
          Contact Us
        </a>
      </div>
    </PublicLayout>
  );
}

function Category({ letter, items }: { letter: string; items: string[] }) {
  return (
    <section className="max-w-7xl mx-auto">
      <h2 className="text-3xl font-bold text-[#014737] mb-6">{letter}</h2>
      <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 xl:grid-cols-4 gap-6">
        {items.map((item) => (
          <div
            key={item}
            className="bg-white border border-gray-200 rounded-lg shadow-sm p-5 hover:shadow-md transition"
          >
            <p className="text-base font-medium text-gray-800">{item}</p>
          </div>
        ))}
      </div>
    </section>
  );
}
