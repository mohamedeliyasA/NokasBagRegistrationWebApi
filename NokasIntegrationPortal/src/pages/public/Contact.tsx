import PublicLayout from "../../layouts/PublicLayout";

export default function Contact() {
  return (
    <PublicLayout>
      <h1 className="text-4xl font-bold mb-12 text-[#014737] text-center">Contact Us</h1>

      <section className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-8 mb-16">
        <div className="bg-white border border-gray-200 rounded p-6 shadow-sm">
          <h2 className="text-xl font-semibold text-[#014737] mb-2">Integration Team</h2>
          <p className="text-gray-700 mb-2">
            Questions about <strong>CashBagRegistration API</strong>?
          </p>
          <p className="font-medium text-sm">Contact: <a href="mailto:BizTalkOps@nokas.no" className="text-blue-700 underline">BizTalkOps@nokas.no</a></p>
        </div>

        <div className="bg-white border border-gray-200 rounded p-6 shadow-sm">
          <h2 className="text-xl font-semibold text-[#014737] mb-2">Customer Service</h2>
          <p className="text-gray-700">Contact customer service</p>
          <p className="font-medium mt-1">915 02580 / +47 915 02580</p>
        </div>

        <div className="bg-white border border-gray-200 rounded p-6 shadow-sm">
          <h2 className="text-xl font-semibold text-[#014737] mb-2">Visiting Address</h2>
          <p>Nokas Value Management AS</p>
          <p>Alf Bjerckes vei 1, 0582 Oslo</p>
        </div>

        <div className="bg-white border border-gray-200 rounded p-6 shadow-sm">
          <h2 className="text-xl font-semibold text-[#014737] mb-2">Postal Address – Head Office</h2>
          <p>Nokas Value Management AS</p>
          <p>PO Box 384 Nydalen, 0513 Oslo</p>
        </div>
      </section>

      <section className="mb-20">
        <h2 className="text-2xl font-bold text-[#014737] mb-6">Nationwide Valuables Transport – Norway</h2>
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          <LocationBlock title="Oslo" lines={[
            "Nokas Verdihåndning AS Head office",
            "Alf Bjerckes vei 1",
            "0582 Oslo",
            "915 02580"
          ]} />
          <LocationBlock title="Rogaland" lines={[
            "Nokas Value Handling AS",
            "Strandsvingen 10",
            "4032 Stavanger",
            "915 02580"
          ]} />
          <LocationBlock title="Troms and Finnmark" lines={[
            "Nokas Value Management AS",
            "Bankgata 9/11",
            "9008 Tromsø",
            "915 02580"
          ]} />
          <LocationBlock title="Trøndelag" lines={[
            "Nokas Value Handling AS",
            "Peter Egges Plass 2",
            "7011 Trondheim",
            "915 02580"
          ]} />
          <LocationBlock title="Westland" lines={[
            "Nokas Value Management AS",
            "Bradbenken 1",
            "5003 Bergen",
            "915 02580"
          ]} />
        </div>
      </section>

      <section className="mb-12">
        <h2 className="text-2xl font-bold text-[#014737] mb-6">Denmark</h2>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
          <LocationBlock title="Brøndby" lines={[
            "Nokas Value Management A/S",
            "Solmarksvej 5",
            "2605 Brøndby",
            "+45 70 15 76 00"
          ]} />
          <LocationBlock title="Brabrand" lines={[
            "Nokas Value Management A/S",
            "Rosbjergvej 15",
            "8220 Brabrand",
            "+45 70 15 76 00"
          ]} />
        </div>
      </section>
    </PublicLayout>
  );
}

function LocationBlock({ title, lines }: { title: string; lines: string[] }) {
  return (
    <div className="bg-gray-50 border border-gray-200 rounded p-5 shadow-sm">
      <h3 className="text-lg font-semibold mb-2 text-[#014737]">{title}</h3>
      {lines.map((line, idx) => (
        <p key={idx} className="text-sm text-gray-800">{line}</p>
      ))}
    </div>
  );
}
